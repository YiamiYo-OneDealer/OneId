using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.TenantAdmin.Roles.Commands;
using OneId.Server.Application.TenantAdmin.Roles.Queries;
using OpenIddict.Validation.AspNetCore;

namespace OneId.Server.Controllers;

[ApiController]
[Route("api/tenant/roles")]
[Authorize(
    AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
    Roles = "TenantAdmin")]
public class TenantRolesController(
    ListRolesHandler listHandler,
    GetRoleHandler getHandler,
    CreateRoleHandler createHandler,
    UpdateRoleHandler updateHandler,
    DeleteRoleHandler deleteHandler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await listHandler.HandleAsync(new ListRolesRequest(page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var dto = await getHandler.HandleAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RoleBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { error = "invalid_name" });

        if (body.PermissionIds is null || body.PermissionIds.Any(id => id is null))
            return BadRequest(new { error = "invalid_permission_ids" });

        try
        {
            var dto = await createHandler.HandleAsync(new CreateRoleRequest(body.Name, body.PermissionIds), ct);
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        }
        catch (InvalidPermissionIdsException ex)
        {
            return UnprocessableEntity(new { error = "invalid_permission_ids", invalidIds = ex.InvalidIds });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] RoleUpdateBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { error = "invalid_name" });

        if (body.PermissionIds is null || body.PermissionIds.Any(id => id is null))
            return BadRequest(new { error = "invalid_permission_ids" });

        try
        {
            var dto = await updateHandler.HandleAsync(
                new UpdateRoleRequest(id, body.Name, body.PermissionIds, body.Version), ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidPermissionIdsException ex)
        {
            return UnprocessableEntity(new { error = "invalid_permission_ids", invalidIds = ex.InvalidIds });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "conflict", detail = "Stale version — reload and retry." });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            var found = await deleteHandler.HandleAsync(id, ct);
            return found ? NoContent() : NotFound();
        }
        catch (RoleInUseException ex)
        {
            return Conflict(new { error = "role_in_use", groups = ex.GroupNames });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "conflict", detail = "Concurrent modification — reload and retry." });
        }
    }
}

public sealed record RoleBody(string Name, IReadOnlyList<string> PermissionIds);
public sealed record RoleUpdateBody(string Name, IReadOnlyList<string> PermissionIds, uint Version);
