using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneId.Server.Application.Audit;
using OpenIddict.Validation.AspNetCore;

namespace OneId.Server.Controllers;

[ApiController]
[Route("api/tenant/audit")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
           Roles = "TenantAdmin")]
public class AuditLogController(IAuditService auditService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 25;

        var result = await auditService.QueryAsync(page, pageSize, ct);
        return Ok(result);
    }
}
