using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneId.Server.Application.Permissions;
using OpenIddict.Validation.AspNetCore;

namespace OneId.Server.Controllers;

[ApiController]
[Route("api/tenant/effective-permissions")]
[Authorize(
    AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
    Roles = "TenantAdmin")]
public class TenantEffectivePermissionsController(
    EffectivePermissionsPreviewHandler previewHandler) : ControllerBase
{
    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] PreviewBody body, CancellationToken ct)
    {
        var request = new PreviewRequest(
            body.GroupIds ?? [],
            body.RoleSets ?? [],
            body.Overrides?.Select(o => new PreviewOverrideEntry(o.PermissionId, o.Effect)).ToList() ?? []);

        var result = await previewHandler.HandleAsync(request, ct);
        return Ok(result);
    }
}

public sealed record PreviewBody(
    List<Guid>? GroupIds,
    List<Guid>? RoleSets,
    List<PreviewOverrideBody>? Overrides);

public sealed record PreviewOverrideBody(string PermissionId, string Effect);
