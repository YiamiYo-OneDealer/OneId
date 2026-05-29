namespace OneId.Server.Application.TenantAdmin.Dimensions;

public sealed record UserDimensionValueDto(Guid Id, string Value);

public sealed record UserDimensionsGroupedDto(
    IReadOnlyList<UserDimensionValueDto> Company,
    IReadOnlyList<UserDimensionValueDto> Location,
    IReadOnlyList<UserDimensionValueDto> Branch,
    IReadOnlyList<UserDimensionValueDto> Make,
    IReadOnlyList<UserDimensionValueDto> MarketSegment);
