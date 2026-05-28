namespace OneId.Server.Domain.Services;

public interface IDimensionEvaluator
{
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> EvaluateAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default);
}
