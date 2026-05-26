namespace OneId.Server.Application.TenantAdmin.Dimensions;

public sealed record UserDimensionAssignmentDto(
    Guid Id,
    Guid UserId,
    Guid DimensionValueId,
    string Axis,
    string Value,
    DateTimeOffset AssignedAt);
