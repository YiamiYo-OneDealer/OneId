using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneId.Server.Application.TenantAdmin.Dimensions.Commands;
using OneId.Server.Application.TenantAdmin.Dimensions.Queries;
using OpenIddict.Validation.AspNetCore;

namespace OneId.Server.Controllers;

[ApiController]
[Route("api/tenant/users/{userId:guid}/dimensions")]
[Authorize(
    AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
    Roles = "TenantAdmin")]
public class TenantUserDimensionsController(
    GetUserDimensionsHandler getHandler,
    AssignUserDimensionHandler assignHandler,
    RemoveUserDimensionHandler removeHandler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid userId, CancellationToken ct)
    {
        try
        {
            var dto = await getHandler.HandleAsync(userId, ct);
            return Ok(dto);
        }
        catch (UserDimensionUserNotFoundException)
        {
            return NotFound(new { error = "user_not_found" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Assign(Guid userId, [FromBody] AssignDimensionBody body, CancellationToken ct)
    {
        try
        {
            var dto = await assignHandler.HandleAsync(userId, body.ValueId, ct);
            return CreatedAtAction(nameof(Get), new { userId }, dto);
        }
        catch (AssignDimensionUserNotFoundException)
        {
            return NotFound(new { error = "user_not_found" });
        }
        catch (InvalidDimensionValueException)
        {
            return UnprocessableEntity(new { error = "invalid_dimension_value" });
        }
        catch (DimensionAlreadyAssignedException)
        {
            return Conflict(new { error = "already_assigned" });
        }
    }

    [HttpDelete("{assignmentId:guid}")]
    public async Task<IActionResult> Remove(Guid userId, Guid assignmentId, CancellationToken ct)
    {
        var found = await removeHandler.HandleAsync(userId, assignmentId, ct);
        return found ? NoContent() : NotFound();
    }
}

public sealed record AssignDimensionBody(Guid ValueId);
