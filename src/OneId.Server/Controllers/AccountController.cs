using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Controllers;

[ApiController]
[Route("account")]
public class AccountController(
    AppDbContext db,
    IEmailSender emailSender,
    IPasswordHasher<User> hasher,
    IUserTokenRevoker revoker,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken ct)
    {
        var user = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.DeletedAt == null, ct);

        if (user is not null)
        {
            var token = Guid.NewGuid().ToString("N");
            user.PasswordResetToken = token;
            user.PasswordResetTokenExpiry = DateTimeOffset.UtcNow.AddHours(1);
            await db.SaveChangesAsync(ct);

            var frontendBase = configuration["App:FrontendBaseUrl"] ?? "http://localhost:3000";
            var resetLink = $"{frontendBase}/reset-password?token={token}";
            await emailSender.SendAsync(
                user.Email,
                "Reset your OneId password",
                $"Click to reset your password: {resetLink}",
                ct);
        }

        return Ok(new { message = "If that email is registered, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(new { error = "password_too_weak" });

        var user = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.PasswordResetToken == request.Token && u.DeletedAt == null, ct);

        if (user is null || user.PasswordResetTokenExpiry < DateTimeOffset.UtcNow)
            return BadRequest(new { error = "invalid_or_expired_token" });

        var verificationResult = hasher.VerifyHashedPassword(
            user,
            user.PasswordHash ?? string.Empty,
            request.NewPassword);

        if (verificationResult != PasswordVerificationResult.Failed)
            return BadRequest(new { error = "password_reuse" });

        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        await revoker.RevokeAllUserTokensAsync(user.Id, ct);

        return Ok();
    }
}

public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Token, string NewPassword);
