using Microsoft.EntityFrameworkCore;
using OneId.Server.Domain.Entities;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.Dimensions.Commands;

public sealed class SetUserDimensionsHandler(AppDbContext db)
{
    public async Task HandleAsync(Guid userId, IReadOnlyList<Guid> valueIds, CancellationToken ct = default)
    {
        var userExists = await db.Users.AnyAsync(u => u.Id == userId, ct);
        if (!userExists) throw new SetDimensionsUserNotFoundException();

        var distinctIds = valueIds.Distinct().ToList();

        // Global filter on DimensionValues scopes to current tenant; IsActive check added explicitly
        var validCount = await db.DimensionValues
            .CountAsync(d => distinctIds.Contains(d.Id) && d.IsActive, ct);
        if (validCount != distinctIds.Count)
            throw new InvalidDimensionValueException();

        var current = await db.UserDimensionAssignments
            .Where(a => a.UserId == userId)
            .ToListAsync(ct);

        var currentSet = current.Select(a => a.DimensionValueId).ToHashSet();
        var desiredSet = distinctIds.ToHashSet();

        db.UserDimensionAssignments.RemoveRange(
            current.Where(a => !desiredSet.Contains(a.DimensionValueId)));

        db.UserDimensionAssignments.AddRange(
            desiredSet
                .Where(id => !currentSet.Contains(id))
                .Select(id => new UserDimensionAssignment
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    DimensionValueId = id,
                    AssignedAt = DateTimeOffset.UtcNow,
                }));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("23505") == true)
        {
            throw new DimensionAssignmentConflictException();
        }
    }
}

public sealed class SetDimensionsUserNotFoundException()
    : Exception("User not found in this tenant.");

public sealed class InvalidDimensionValueException()
    : Exception("One or more dimension values are inactive or belong to a different tenant.");

public sealed class DimensionAssignmentConflictException()
    : Exception("Concurrent assignment conflict; please retry.");
