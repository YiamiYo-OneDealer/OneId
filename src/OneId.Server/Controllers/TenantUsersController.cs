using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneId.Server.Application.Common;
using OneId.Server.Application.Permissions;
using OneId.Server.Application.TenantAdmin.Groups.Queries;
using OneId.Server.Application.TenantAdmin.Users.Commands;
using OneId.Server.Application.TenantAdmin.Users.Queries;
using OpenIddict.Validation.AspNetCore;

namespace OneId.Server.Controllers;

[ApiController]
[Route("api/tenant/users")]
[Authorize(
    AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
    Roles = "TenantAdmin,InternalAdmin")]
public class TenantUsersController(
    ListUsersHandler listHandler,
    GetUserHandler getHandler,
    CreateUserHandler createHandler,
    UpdateUserHandler updateHandler,
    DeleteUserHandler deleteHandler,
    GetEffectivePermissionsHandler effectivePermissionsHandler,
    GetUserGroupsHandler getUserGroupsHandler,
    IUserTokenRevoker tokenRevoker) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var result = await listHandler.HandleAsync(new ListUsersRequest(page, pageSize, includeInactive), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var dto = await getHandler.HandleAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserBody body, CancellationToken ct)
    {
        try
        {
            var dto = await createHandler.HandleAsync(
                new CreateUserRequest(body.Email, body.DisplayName, body.Password), ct);
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        }
        catch (UserEmailConflictException)
        {
            return Conflict(new { error = "email_conflict" });
        }
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserBody body, CancellationToken ct)
    {
        try
        {
            var dto = await updateHandler.HandleAsync(new UpdateUserRequest(id, body.DisplayName, body.Email), ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (UserEmailConflictException)
        {
            return Conflict(new { error = "email_conflict" });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var found = await deleteHandler.HandleAsync(id, ct);
        return found ? NoContent() : NotFound();
    }

    [HttpGet("{id:guid}/effective-permissions")]
    public async Task<IActionResult> GetEffectivePermissions(Guid id, CancellationToken ct)
    {
        var result = await effectivePermissionsHandler.HandleAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/groups")]
    public async Task<IActionResult> GetUserGroups(Guid id, CancellationToken ct)
    {
        try
        {
            var groups = await getUserGroupsHandler.HandleAsync(id, ct);
            return Ok(new { items = groups });
        }
        catch (GetUserGroupsUserNotFoundException)
        {
            return NotFound(new { error = "user_not_found" });
        }
    }

    [HttpPost("{id:guid}/revoke-tokens")]
    public async Task<IActionResult> RevokeTokens(Guid id, CancellationToken ct)
    {
        var user = await getHandler.HandleAsync(id, ct);
        if (user is null) return NotFound();

        await tokenRevoker.RevokeAllUserTokensAsync(id, ct);
        return NoContent();
    }
}

public sealed record CreateUserBody([Required][MaxLength(320)][EmailAddress] string Email, string? DisplayName, string? Password);
public sealed record UpdateUserBody(string? DisplayName, string? Email);
