using Microsoft.EntityFrameworkCore;
using OneId.Server.Domain.Entities;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.Dimensions.Commands;

public sealed class AssignUserDimensionHandler(AppDbContext db)
{
    public async Task<UserDimensionAssignmentDto> HandleAsync(Guid userId, Guid valueId, CancellationToken ct = default)
    {
        var userExists = await db.Users.AnyAsync(u => u.Id == userId, ct);
        if (!userExists) throw new AssignDimensionUserNotFoundException();

        // Global query filter on DimensionValues scopes to current tenant; IsActive check added explicitly
        var dimValue = await db.DimensionValues
            .FirstOrDefaultAsync(d => d.Id == valueId && d.IsActive, ct);
        if (dimValue is null) throw new InvalidDimensionValueException();

        var alreadyAssigned = await db.UserDimensionAssignments
            .AnyAsync(a => a.UserId == userId && a.DimensionValueId == valueId, ct);
        if (alreadyAssigned) throw new DimensionAlreadyAssignedException();

        var assignment = new UserDimensionAssignment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DimensionValueId = valueId,
            AssignedAt = DateTimeOffset.UtcNow,
        };

        db.UserDimensionAssignments.Add(assignment);
        await db.SaveChangesAsync(ct);

        return new UserDimensionAssignmentDto(
            assignment.Id,
            userId,
            valueId,
            dimValue.Axis.ToString(),
            dimValue.Value,
            assignment.AssignedAt);
    }
}

public sealed class AssignDimensionUserNotFoundException()
    : Exception("User not found in this tenant.");

public sealed class InvalidDimensionValueException()
    : Exception("DimensionValue is inactive or belongs to a different tenant.");

public sealed class DimensionAlreadyAssignedException()
    : Exception("This dimension value is already assigned to the user.");
