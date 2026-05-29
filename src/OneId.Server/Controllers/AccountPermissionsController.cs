using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneId.Server.Domain.Services;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;

namespace OneId.Server.Controllers;

[ApiController]
[Route("api/account")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public class AccountPermissionsController(IPermissionEvaluator permissionEvaluator) : ControllerBase
{
    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions(CancellationToken ct)
    {
        var subClaim = User.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
        var tidClaim = User.FindFirst("tid")?.Value;

        if (!Guid.TryParse(subClaim, out var userId) || !Guid.TryParse(tidClaim, out var tenantId))
            return Unauthorized();

        var permissions = await permissionEvaluator.EvaluateAsync(userId, tenantId, ct);
        return Ok(new { permissions = permissions.ToArray() });
    }
}
