namespace OneId.Server.Domain.Services;

public interface IPermissionEvaluator
{
    Task<IReadOnlySet<string>> EvaluateAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
}
