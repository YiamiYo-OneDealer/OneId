using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.TenantAdmin.RoleSets.Commands;
using OneId.Server.Application.TenantAdmin.RoleSets.Queries;
using OpenIddict.Validation.AspNetCore;

namespace OneId.Server.Controllers;

[ApiController]
[Route("api/tenant/role-sets")]
[Authorize(
    AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
    Roles = "TenantAdmin")]
public class TenantRoleSetsController(
    ListRoleSetsHandler listHandler,
    GetRoleSetHandler getHandler,
    CreateRoleSetHandler createHandler,
    UpdateRoleSetHandler updateHandler,
    DeleteRoleSetHandler deleteHandler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await listHandler.HandleAsync(new ListRoleSetsRequest(page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var dto = await getHandler.HandleAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RoleSetBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { error = "invalid_name" });

        try
        {
            var dto = await createHandler.HandleAsync(new CreateRoleSetRequest(body.Name, body.RoleIds), ct);
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        }
        catch (InvalidRoleIdsException ex)
        {
            return UnprocessableEntity(new { error = "invalid_role_ids", invalidIds = ex.InvalidIds });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] RoleSetUpdateBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { error = "invalid_name" });

        try
        {
            var dto = await updateHandler.HandleAsync(
                new UpdateRoleSetRequest(id, body.Name, body.RoleIds, body.Version), ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidRoleIdsException ex)
        {
            return UnprocessableEntity(new { error = "invalid_role_ids", invalidIds = ex.InvalidIds });
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
        catch (RoleSetInUseException ex)
        {
            return Conflict(new { error = "role_set_in_use", groups = ex.GroupNames });
        }
    }
}

public sealed record RoleSetBody(string Name, IReadOnlyList<Guid> RoleIds);
public sealed record RoleSetUpdateBody(string Name, IReadOnlyList<Guid> RoleIds, uint Version);
