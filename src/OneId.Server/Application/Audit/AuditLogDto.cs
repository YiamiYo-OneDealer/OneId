namespace OneId.Server.Application.Audit;

public sealed record AuditLogDto(
    Guid Id,
    Guid TenantId,
    Guid? ActorUserId,
    string Action,
    string EntityType,
    Guid EntityId,
    string? Payload,
    DateTimeOffset Timestamp);
