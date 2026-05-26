namespace OneId.Server.Application.TenantAdmin.Dimensions;

public sealed record UserDimensionsGroupedDto(
    IReadOnlyList<string> Company,
    IReadOnlyList<string> Location,
    IReadOnlyList<string> Branch,
    IReadOnlyList<string> Make,
    IReadOnlyList<string> MarketSegment);
