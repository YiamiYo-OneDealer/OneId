namespace OneId.Server.Application.Common;

public interface IUserTokenRevoker
{
    Task RevokeAllUserTokensAsync(Guid userId, CancellationToken ct = default);
}
