namespace OneId.Server.Application.Audit;

public sealed record AuditLogEntry(
    Guid TenantId,
    string Action,
    string EntityType,
    Guid EntityId,
    string? Payload = null);
