using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.TenantAdmin.Groups.Commands;
using OneId.Server.Application.TenantAdmin.Groups.Queries;
using OpenIddict.Validation.AspNetCore;

namespace OneId.Server.Controllers;

[ApiController]
[Route("api/tenant/groups")]
[Authorize(
    AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
    Roles = "TenantAdmin")]
public class TenantGroupsController(
    ListGroupsHandler listHandler,
    GetGroupHandler getHandler,
    CreateGroupHandler createHandler,
    UpdateGroupHandler updateHandler,
    DeleteGroupHandler deleteHandler,
    AddGroupMemberHandler addMemberHandler,
    RemoveGroupMemberHandler removeMemberHandler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await listHandler.HandleAsync(new ListGroupsRequest(page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var dto = await getHandler.HandleAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] GroupBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { error = "invalid_name" });

        try
        {
            var dto = await createHandler.HandleAsync(
                new CreateGroupRequest(body.Name, body.RoleIds, body.RoleSetIds), ct);
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        }
        catch (InvalidRoleIdsException ex)
        {
            return UnprocessableEntity(new { error = "invalid_role_ids", invalidIds = ex.InvalidIds });
        }
        catch (InvalidRoleSetIdsException ex)
        {
            return UnprocessableEntity(new { error = "invalid_role_set_ids", invalidIds = ex.InvalidIds });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] GroupUpdateBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { error = "invalid_name" });

        try
        {
            var dto = await updateHandler.HandleAsync(
                new UpdateGroupRequest(id, body.Name, body.RoleIds, body.RoleSetIds, body.Version), ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidRoleIdsException ex)
        {
            return UnprocessableEntity(new { error = "invalid_role_ids", invalidIds = ex.InvalidIds });
        }
        catch (InvalidRoleSetIdsException ex)
        {
            return UnprocessableEntity(new { error = "invalid_role_set_ids", invalidIds = ex.InvalidIds });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "conflict", detail = "Stale version — reload and retry." });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var found = await deleteHandler.HandleAsync(id, ct);
        return found ? NoContent() : NotFound();
    }

    [HttpPut("{id:guid}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberBody body, CancellationToken ct)
    {
        var result = await addMemberHandler.HandleAsync(new AddGroupMemberRequest(id, body.UserId), ct);
        return result switch
        {
            AddMemberResult.Ok => Ok(),
            AddMemberResult.GroupNotFound => NotFound(),
            AddMemberResult.UserNotFound => NotFound(),
            _ => StatusCode(500),
        };
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        var result = await removeMemberHandler.HandleAsync(id, userId, ct);
        return result switch
        {
            RemoveMemberResult.Ok => NoContent(),
            RemoveMemberResult.NotFound => NotFound(),
            _ => StatusCode(500),
        };
    }
}

public sealed record GroupBody(string Name, IReadOnlyList<Guid> RoleIds, IReadOnlyList<Guid> RoleSetIds);
public sealed record GroupUpdateBody(string Name, IReadOnlyList<Guid> RoleIds, IReadOnlyList<Guid> RoleSetIds, uint Version);
public sealed record AddMemberBody(Guid UserId);
