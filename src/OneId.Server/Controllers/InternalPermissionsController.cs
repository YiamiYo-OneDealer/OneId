using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Internal.Permissions.Commands;
using OneId.Server.Application.Internal.Permissions.Queries;
using OpenIddict.Validation.AspNetCore;

namespace OneId.Server.Controllers;

[ApiController]
[Route("api/internal/permissions")]
[Authorize(
    AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
    Roles = "InternalAdmin")]
public class InternalPermissionsController(
    ListPermissionsHandler listHandler,
    GetPermissionHandler getHandler,
    CreatePermissionHandler createHandler,
    UpdatePermissionHandler updateHandler,
    DeactivatePermissionHandler deactivateHandler,
    ReactivatePermissionHandler reactivateHandler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string status = "Active",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await listHandler.HandleAsync(new ListPermissionsRequest(status, page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{permissionId}")]
    public async Task<IActionResult> Get(string permissionId, CancellationToken ct)
    {
        var dto = await getHandler.HandleAsync(permissionId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePermissionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.PermissionId) || string.IsNullOrWhiteSpace(request.Label))
            return BadRequest(new { error = "invalid_request" });

        try
        {
            var dto = await createHandler.HandleAsync(request, ct);
            return CreatedAtAction(nameof(Get), new { permissionId = dto.PermissionId }, dto);
        }
        catch (PermissionIdTakenException)
        {
            return Conflict(new { error = "permission_id_taken" });
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("23505") == true
                                         || ex.InnerException?.Message.Contains("unique") == true)
        {
            return Conflict(new { error = "permission_id_taken" });
        }
    }

    [HttpPatch("{permissionId}")]
    public async Task<IActionResult> Update(string permissionId, [FromBody] UpdatePermissionBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Label))
            return BadRequest(new { error = "invalid_request" });

        try
        {
            var dto = await updateHandler.HandleAsync(new UpdatePermissionRequest(permissionId, body.Label, body.Version), ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "conflict", detail = "Stale version — reload and retry." });
        }
    }

    [HttpDelete("{permissionId}")]
    public async Task<IActionResult> Deactivate(string permissionId, CancellationToken ct)
    {
        var found = await deactivateHandler.HandleAsync(permissionId, ct);
        return found ? NoContent() : NotFound();
    }

    [HttpPost("{permissionId}/activate")]
    public async Task<IActionResult> Activate(string permissionId, CancellationToken ct)
    {
        var found = await reactivateHandler.HandleAsync(permissionId, ct);
        return found ? NoContent() : NotFound();
    }
}

public sealed record UpdatePermissionBody(string Label, uint Version);
