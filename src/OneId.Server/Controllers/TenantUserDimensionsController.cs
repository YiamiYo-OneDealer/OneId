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
    SetUserDimensionsHandler setHandler) : ControllerBase
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

    [HttpPut]
    public async Task<IActionResult> Set(Guid userId, [FromBody] SetDimensionsBody body, CancellationToken ct)
    {
        try
        {
            await setHandler.HandleAsync(userId, body.ValueIds, ct);
            return NoContent();
        }
        catch (SetDimensionsUserNotFoundException)
        {
            return NotFound(new { error = "user_not_found" });
        }
        catch (InvalidDimensionValueException)
        {
            return UnprocessableEntity(new { error = "invalid_dimension_value" });
        }
        catch (DimensionAssignmentConflictException)
        {
            return Conflict(new { error = "assignment_conflict" });
        }
    }
}

public sealed record SetDimensionsBody(IReadOnlyList<Guid> ValueIds);
