namespace OneId.Server.Application.TokenPipeline;

public sealed record TokenEnrichmentContext(Guid UserId, Guid TenantId, string? GrantType);
