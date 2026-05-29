using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneId.Server.Application.TenantAdmin.Dimensions.Commands;
using OneId.Server.Application.TenantAdmin.Dimensions.Queries;
using OneId.Server.Domain.Enums;
using OpenIddict.Validation.AspNetCore;

namespace OneId.Server.Controllers;

[ApiController]
[Route("api/tenant/dimensions/{axis}/values")]
[Authorize(
    AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
    Roles = "TenantAdmin")]
public class TenantDimensionsController(
    ListDimensionValuesHandler listHandler,
    AddDimensionValueHandler addHandler,
    DeactivateDimensionValueHandler deactivateHandler) : ControllerBase
{
    private static readonly DimensionAxis[] AllAxes =
        [DimensionAxis.Company, DimensionAxis.Location, DimensionAxis.Branch, DimensionAxis.Make, DimensionAxis.MarketSegment];

    private static bool TryParseAxis(string raw, out DimensionAxis axis)
        => Enum.TryParse(raw, ignoreCase: true, out axis)
           && !char.IsDigit(raw[0]);

    [HttpGet("/api/tenant/dimensions")]
    public async Task<IActionResult> ListAll(CancellationToken ct)
    {
        var grouped = new Dictionary<string, object>();
        foreach (var axis in AllAxes)
            grouped[axis.ToString()] = await listHandler.HandleAsync(axis, ct);
        return Ok(grouped);
    }

    [HttpGet]
    public async Task<IActionResult> List(string axis, CancellationToken ct)
    {
        if (!TryParseAxis(axis, out var parsedAxis))
            return BadRequest(new { error = "invalid_axis" });
        var result = await listHandler.HandleAsync(parsedAxis, ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Add(string axis, [FromBody] AddDimensionValueBody body, CancellationToken ct)
    {
        if (!TryParseAxis(axis, out var parsedAxis))
            return BadRequest(new { error = "invalid_axis" });
        if (string.IsNullOrWhiteSpace(body.Value))
            return BadRequest(new { error = "invalid_value" });
        if (body.Value.Trim().Length > 200)
            return BadRequest(new { error = "value_too_long" });
        try
        {
            var dto = await addHandler.HandleAsync(parsedAxis, body.Value, ct);
            return CreatedAtAction(nameof(List), new { axis }, dto);
        }
        catch (DuplicateDimensionValueException)
        {
            return Conflict(new { error = "duplicate_value" });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(string axis, Guid id, CancellationToken ct)
    {
        if (!TryParseAxis(axis, out var parsedAxis))
            return BadRequest(new { error = "invalid_axis" });
        var found = await deactivateHandler.HandleAsync(id, parsedAxis, ct);
        return found ? NoContent() : NotFound();
    }
}

public sealed record AddDimensionValueBody(string Value);
