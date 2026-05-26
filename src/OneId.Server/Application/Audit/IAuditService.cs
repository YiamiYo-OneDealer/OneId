namespace OneId.Server.Application.Audit;

public interface IAuditService
{
    Task AppendAsync(AuditLogEntry entry, CancellationToken ct = default);
    Task<PagedResponse<AuditLogDto>> QueryAsync(int page, int pageSize, CancellationToken ct = default);
}
