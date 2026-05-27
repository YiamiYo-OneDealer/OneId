using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.Users.Commands;

public sealed class UserEmailConflictException() : Exception("Email already exists in this tenant.");

public sealed record CreateUserRequest(string Email, string? DisplayName, string? Password);

public sealed class CreateUserHandler(
    AppDbContext db,
    ITenantContext tenantContext,
    IAuditService audit,
    IPasswordHasher<User> hasher)
{
    public async Task<UserDto> HandleAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var emailExists = await db.Users.AnyAsync(u => u.Email == request.Email, ct);
        if (emailExists) throw new UserEmailConflictException();

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            Email = request.Email,
            DisplayName = request.DisplayName,
            CreatedAt = now,
            UpdatedAt = now,
        };

        if (request.Password is not null)
            user.PasswordHash = hasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        await audit.AppendAsync(new AuditLogEntry(
            tenantContext.TenantId, "user.created", "User", user.Id), ct);
        await db.SaveChangesAsync(ct);

        return new UserDto(user.Id, user.Email, user.DisplayName, user.TenantId,
            true, user.IsTenantAdmin, user.CreatedAt, user.UpdatedAt);
    }
}
