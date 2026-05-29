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
        const int maxListSize = 100;
        if ((body.GroupIds?.Count ?? 0) > maxListSize ||
            (body.RoleSets?.Count ?? 0) > maxListSize ||
            (body.Overrides?.Count ?? 0) > maxListSize)
            return BadRequest(new { error = $"Each list field may contain at most {maxListSize} items." });

        if (body.Overrides?.Any(o => string.IsNullOrEmpty(o.PermissionId) ||
                !string.Equals(o.Effect, "DENY", StringComparison.OrdinalIgnoreCase)) == true)
            return BadRequest(new { error = "Each override must have a non-empty permissionId and effect must be \"DENY\"." });

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
