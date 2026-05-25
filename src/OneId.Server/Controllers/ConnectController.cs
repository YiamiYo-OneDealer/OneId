using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OneId.Server.Domain.Entities;
using OneId.Server.Infrastructure.Persistence;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OneId.Server.Controllers;

[ApiController]
public class ConnectController(AppDbContext db, IPasswordHasher<User> hasher) : ControllerBase
{
    [HttpPost("~/connect/token")]
    [Consumes("application/x-www-form-urlencoded")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Token(CancellationToken ct)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict server request is null.");

        if (!request.IsPasswordGrantType())
            return Forbid(
                BuildForbidProperties("unsupported_grant_type", "The grant type is not supported."),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var username = request.Username ?? string.Empty;

        // Look up user ignoring tenant filter — tenant context not yet available during password grant
        var user = await db.Users.IgnoreQueryFilters()
            .Where(u => u.Email == username && u.DeletedAt == null)
            .FirstOrDefaultAsync(ct);

        // Fast path for unknown user — return same generic error without exposing user existence
        if (user is null)
            return ForbidInvalidGrant();

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

        // Success — reset lockout counters
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        await db.SaveChangesAsync(ct);

        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParameters.DefaultAuthenticationType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, user.Id.ToString())
                .SetClaim(Claims.Email, user.Email)
                .SetClaim("tid", user.TenantId.ToString());

        var principal = new ClaimsPrincipal(identity);

        principal.SetScopes(request.GetScopes());

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
            user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(5);

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
