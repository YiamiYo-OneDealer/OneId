namespace OneId.Server.Application.TenantAdmin.Dimensions;

public sealed record DimensionValueDto(Guid Id, string Axis, string Value, uint Version);
