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
    DeactivateTenantHandler deactivateHandler) : ControllerBase
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
}
