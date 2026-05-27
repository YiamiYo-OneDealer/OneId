using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneId.Server.Application.TenantAdmin.UserOverrides;
using OneId.Server.Application.TenantAdmin.UserOverrides.Commands;
using OneId.Server.Application.TenantAdmin.UserOverrides.Queries;
using OneId.Server.Domain.Enums;
using OpenIddict.Validation.AspNetCore;

namespace OneId.Server.Controllers;

[ApiController]
[Route("api/tenant/users/{userId:guid}/overrides")]
[Authorize(
    AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
    Roles = "TenantAdmin")]
public class TenantUserOverridesController(
    ListUserOverridesHandler listHandler,
    CreateUserOverrideHandler createHandler,
    DeleteUserOverrideHandler deleteHandler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid userId, CancellationToken ct)
    {
        var result = await listHandler.HandleAsync(userId, ct);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid userId, [FromBody] CreateOverrideBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Reason))
            return UnprocessableEntity(new { error = "reason_required" });

        if (!Enum.TryParse<PermissionOverrideType>(body.OverrideType, ignoreCase: true, out var overrideType))
            return UnprocessableEntity(new { error = "invalid_override_type" });

        try
        {
            var dto = await createHandler.HandleAsync(
                new CreateUserOverrideRequest(userId, body.PermissionId, overrideType, body.Reason!, body.ExpiresAt), ct);
            if (dto is null) return NotFound();
            return CreatedAtAction(nameof(List), new { userId }, dto);
        }
        catch (InvalidOperationException ex) when (ex.Message == "permission_not_found_or_inactive")
        {
            return UnprocessableEntity(new { error = "permission_not_found_or_inactive" });
        }
        catch (UserOverrideDuplicateException)
        {
            return Conflict(new { error = "duplicate_override" });
        }
    }

    [HttpDelete("{overrideId:guid}")]
    public async Task<IActionResult> Delete(Guid userId, Guid overrideId, CancellationToken ct)
    {
        var found = await deleteHandler.HandleAsync(userId, overrideId, ct);
        return found ? NoContent() : NotFound();
    }
}

public sealed record CreateOverrideBody(
    [Required][MaxLength(200)] string PermissionId,
    [Required] string OverrideType,
    string? Reason,
    DateTimeOffset? ExpiresAt);
