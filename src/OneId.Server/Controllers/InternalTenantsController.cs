using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Internal;
using OneId.Server.Application.Internal.Commands;
using OneId.Server.Application.Internal.Queries;
using OpenIddict.Validation.AspNetCore;

namespace OneId.Server.Controllers;

// TODO Epic 4a: replace with [Authorize(Policy = "InternalAdmin")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
[ApiController]
[Route("api/internal/tenants")]
public class InternalTenantsController(
    ListTenantsHandler listHandler,
    GetTenantHandler getHandler,
    CreateTenantHandler createHandler,
    UpdateTenantHandler updateHandler,
    DeactivateTenantHandler deactivateHandler,
    DesignateTenantAdminHandler designateAdminHandler,
    RemoveTenantAdminHandler removeAdminHandler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var tenants = await listHandler.HandleAsync(ct);
        return Ok(tenants);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var tenant = await getHandler.HandleAsync(id, ct);
        return tenant is null ? NotFound() : Ok(tenant);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
            return BadRequest(new { error = "invalid_name" });

        try
        {
            var tenant = await createHandler.HandleAsync(request, ct);
            return CreatedAtAction(nameof(Get), new { id = tenant.Id }, tenant);
        }
        catch (NameTakenException)
        {
            return BadRequest(new { error = "name_taken" });
        }
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTenantRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
            return BadRequest(new { error = "invalid_name" });

        try
        {
            var tenant = await updateHandler.HandleAsync(id, request, ct);
            return tenant is null ? NotFound() : Ok(tenant);
        }
        catch (NameTakenException)
        {
            return BadRequest(new { error = "name_taken" });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Conflict",
                Detail = "The tenant was modified by another request. Fetch the latest version and retry.",
                Status = StatusCodes.Status409Conflict,
            });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var found = await deactivateHandler.HandleAsync(id, ct);
        return found ? NoContent() : NotFound();
    }

    [HttpPost("{tenantId:guid}/admins/{userId:guid}")]
    public async Task<IActionResult> DesignateAdmin(Guid tenantId, Guid userId, CancellationToken ct)
    {
        var result = await designateAdminHandler.HandleAsync(new DesignateTenantAdminRequest(tenantId, userId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{tenantId:guid}/admins/{userId:guid}")]
    public async Task<IActionResult> RemoveAdmin(Guid tenantId, Guid userId, CancellationToken ct)
    {
        try
        {
            var result = await removeAdminHandler.HandleAsync(tenantId, userId, ct);
            return result is null ? NotFound() : NoContent();
        }
        catch (LastTenantAdminException)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Conflict",
                Detail = "Cannot remove the last Tenant Admin from a tenant.",
                Status = StatusCodes.Status409Conflict,
                Extensions = { ["error"] = "last_tenant_admin" },
            });
        }
    }
}
