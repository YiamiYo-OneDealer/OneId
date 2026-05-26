using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OneId.Server.Application.TokenPipeline;
using OneId.Server.Domain.Entities;
using OneId.Server.Domain.Enums;
using OneId.Server.Infrastructure.Persistence;
using OtpNet;
using System.Security.Claims;
using System.Security.Cryptography;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OneId.Server.Controllers;

[ApiController]
public class ConnectController(
    AppDbContext db,
    IPasswordHasher<User> hasher,
    IDataProtectionProvider dp,
    IEnumerable<ITokenClaimsEnricher> enrichers) : ControllerBase
{
    private IDataProtector TotpProtector => dp.CreateProtector("totp.secret.v1");
    private ITimeLimitedDataProtector MfaSessionProtector =>
        dp.CreateProtector("mfa.session.v1").ToTimeLimitedDataProtector();

    [HttpPost("~/connect/token")]
    [Consumes("application/x-www-form-urlencoded")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Token(CancellationToken ct)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict server request is null.");

        if (request.IsPasswordGrantType())
            return await HandlePasswordGrantAsync(request, ct);

        if (request.GrantType == "urn:oneid:mfa")
            return await HandleMfaGrantAsync(request, ct);

        if (request.IsRefreshTokenGrantType())
            return await HandleRefreshTokenGrantAsync(ct);

        return Forbid(
            BuildForbidProperties("unsupported_grant_type", "The grant type is not supported."),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandlePasswordGrantAsync(OpenIddictRequest request, CancellationToken ct)
    {
        var username = request.Username ?? string.Empty;

        // Look up user ignoring tenant filter — tenant context not yet available during password grant
        var user = await db.Users.IgnoreQueryFilters()
            .Where(u => u.Email == username && u.DeletedAt == null)
            .FirstOrDefaultAsync(ct);

        // Fast path for unknown user — return same generic error without exposing user existence
        if (user is null)
            return ForbidInvalidGrant();

        // Story 3.2: Block token issuance for users whose tenant has been deactivated
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == user.TenantId, ct);

        if (tenant is null || tenant.DeletedAt.HasValue)
            return Forbid(
                BuildForbidProperties(Errors.AccessDenied, "Tenant account has been deactivated."),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        if (tenant.Status == TenantStatus.Suspended)
            return Forbid(
                BuildForbidProperties("tenant_suspended", "This tenant account has been suspended. Contact your administrator."),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        // Check lockout before verifying password
        if (IsLockedOut(user))
            return ForbidInvalidGrant();

        // Verify password
        var passwordResult = hasher.VerifyHashedPassword(
            user,
            user.PasswordHash ?? string.Empty,
            request.Password ?? string.Empty);

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            await IncrementFailedAccessAsync(user, ct);
            return ForbidInvalidGrant();
        }

        // Reset lockout counters on successful password check
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        await db.SaveChangesAsync(ct);

        // MFA gate: if enrolled (or not yet enrolled), require a TOTP challenge step
        var mfaToken = MfaSessionProtector.Protect(user.Id.ToString(), TimeSpan.FromMinutes(5));

        if (user.IsTotpEnrolled)
        {
            return Ok(new
            {
                mfa_required = true,
                mfa_session_token = mfaToken,
            });
        }

        // Not enrolled yet — return enrollment URI so client can set up authenticator
        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secretBytes);

        // Persist the unprotected secret (will be confirmed + protected in MFA grant step)
        // Store base32 plaintext temporarily protected so we can read it back in MFA step
        user.TotpSecret = TotpProtector.Protect(base32Secret);
        await db.SaveChangesAsync(ct);

        var enrollmentUri = $"otpauth://totp/OneId:{Uri.EscapeDataString(user.Email)}?secret={base32Secret}&issuer=OneId";

        return Ok(new
        {
            mfa_required = true,
            mfa_session_token = mfaToken,
            totp_enrollment_uri = enrollmentUri,
        });
    }

    private async Task<IActionResult> HandleMfaGrantAsync(OpenIddictRequest request, CancellationToken ct)
    {
        var mfaSessionToken = (string?)request.GetParameter("mfa_session_token");
        var totpCode = (string?)request.GetParameter("totp_code");

        if (string.IsNullOrEmpty(mfaSessionToken) || string.IsNullOrEmpty(totpCode))
            return ForbidInvalidGrant();

        // Validate the time-limited session token (expires in 5 min)
        string userId;
        try
        {
            userId = MfaSessionProtector.Unprotect(mfaSessionToken);
        }
        catch (CryptographicException)
        {
            return ForbidInvalidGrant();
        }

        if (!Guid.TryParse(userId, out var userGuid))
            return ForbidInvalidGrant();

        var user = await db.Users.IgnoreQueryFilters()
            .Where(u => u.Id == userGuid && u.DeletedAt == null)
            .FirstOrDefaultAsync(ct);

        if (user is null || string.IsNullOrEmpty(user.TotpSecret))
            return ForbidInvalidGrant();

        var mfaTenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == user.TenantId, ct);

        if (mfaTenant?.Status == TenantStatus.Suspended)
            return Forbid(
                BuildForbidProperties("tenant_suspended", "This tenant account has been suspended. Contact your administrator."),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        // Decrypt TOTP secret and verify code
        string base32Secret;
        try
        {
            base32Secret = TotpProtector.Unprotect(user.TotpSecret);
        }
        catch (CryptographicException)
        {
            return ForbidInvalidGrant();
        }

        var totp = new Totp(Base32Encoding.ToBytes(base32Secret));
        var isValid = totp.VerifyTotp(
            totpCode,
            out long timeStepMatched,
            new VerificationWindow(previous: 1, future: 1));

        if (!isValid)
            return ForbidInvalidGrant();

        // Replay prevention: reject if same time step was already used
        if (user.TotpLastUsedTimeStep.HasValue && user.TotpLastUsedTimeStep.Value == timeStepMatched)
            return ForbidInvalidGrant();

        // Mark enrolled (first-time enrollment completes here) and record used time step
        user.IsTotpEnrolled = true;
        user.TotpLastUsedTimeStep = timeStepMatched;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Accept optimistic loss — replay prevention still triggers on subsequent requests
        }

        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParameters.DefaultAuthenticationType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, user.Id.ToString())
                .SetClaim(Claims.Email, user.Email)
                .SetClaim("tid", user.TenantId.ToString());

        var principal = new ClaimsPrincipal(identity);

        principal.SetScopes(request.GetScopes());

        // Run the ITokenClaimsEnricher pipeline — adds roles[], permissions[], etc. in future epics.
        // Enrichers are registered in DI in execution order (Epic 2: RoleClaimsEnricher only).
        var enrichmentContext = new TokenEnrichmentContext(user.Id, user.TenantId, request.GrantType);
        foreach (var enricher in enrichers)
            await enricher.EnrichAsync(identity, enrichmentContext, ct);

        // Destination sweep MUST remain after the enricher pipeline so enricher-added claims are included.
        foreach (var claim in identity.Claims)
            claim.SetDestinations(Destinations.AccessToken);

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandleRefreshTokenGrantAsync(CancellationToken ct)
    {
        // Authenticate using the incoming refresh token — OpenIddict validates it and extracts claims.
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var identity = new ClaimsIdentity(
            result.Principal!.Claims,
            authenticationType: TokenValidationParameters.DefaultAuthenticationType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(result.Principal!.GetScopes());

        // Re-run the enricher pipeline so claims stay current (e.g. role changes reflected on next token).
        var subjectClaim = identity.FindFirst(Claims.Subject)?.Value;
        var tidClaim = identity.FindFirst("tid")?.Value;
        if (subjectClaim is not null && Guid.TryParse(subjectClaim, out var userId)
            && tidClaim is not null && Guid.TryParse(tidClaim, out var tenantId))
        {
            var enrichmentContext = new TokenEnrichmentContext(userId, tenantId, OpenIddictConstants.GrantTypes.RefreshToken);
            foreach (var enricher in enrichers)
                await enricher.EnrichAsync(identity, enrichmentContext, ct);
        }

        foreach (var claim in identity.Claims)
            claim.SetDestinations(Destinations.AccessToken);

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static bool IsLockedOut(User user)
        => user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

    private async Task IncrementFailedAccessAsync(User user, CancellationToken ct)
    {
        user.AccessFailedCount++;
        if (user.AccessFailedCount >= 5)
        {
            user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);
            user.AccessFailedCount = 0;
        }

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { /* Accept optimistic loss — lockout still triggers eventually */ }
    }

    private IActionResult ForbidInvalidGrant() =>
        Forbid(BuildForbidProperties(Errors.InvalidGrant, "Invalid credentials."),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

    private static AuthenticationProperties BuildForbidProperties(string error, string description) =>
        new(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        });
}
