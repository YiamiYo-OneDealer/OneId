using OneId.Server.Application.Common;
using OpenIddict.Abstractions;

namespace OneId.Server.Infrastructure.OpenIddict;

public sealed class RevocationHandler : IUserTokenRevoker
{
    private readonly IOpenIddictTokenManager _tokenManager;

    public RevocationHandler(IOpenIddictTokenManager tokenManager)
    {
        _tokenManager = tokenManager;
    }

    public async Task RevokeAllUserTokensAsync(Guid userId, CancellationToken ct = default)
    {
        await foreach (var token in _tokenManager.FindBySubjectAsync(userId.ToString(), ct))
        {
            await _tokenManager.TryRevokeAsync(token, ct);
        }
    }
}
