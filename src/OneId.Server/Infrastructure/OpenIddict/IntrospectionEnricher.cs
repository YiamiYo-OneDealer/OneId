using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using System.Text.Json;
using System.Text.Json.Nodes;
using OneId.Server.Domain.Services;
using OneId.Server.Infrastructure.Persistence;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace OneId.Server.Infrastructure.OpenIddict;

// Stage 1: Runs during HandleIntrospectionRequestContext while GenericTokenPrincipal is available.
// Evaluates permissions and dimensions, stores results on the transaction for Stage 2.
// Skips if principal is null (inactive/revoked/expired token).
public sealed class IntrospectionDataEnricher(
    IDimensionEvaluator dimensionEvaluator,
    IPermissionEvaluator permissionEvaluator,
    AppDbContext db)
    : IOpenIddictServerHandler<HandleIntrospectionRequestContext>
{
    internal const string PermissionsKey  = "oneid_enriched_permissions";
    internal const string DimensionsKey   = "oneid_enriched_dimensions";
    internal const string GroupsKey       = "oneid_enriched_groups";
    internal const string RolesKey        = "oneid_enriched_roles";

    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<HandleIntrospectionRequestContext>()
            .UseScopedHandler<IntrospectionDataEnricher>()
            .SetOrder(int.MaxValue - 100)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(HandleIntrospectionRequestContext context)
    {
        if (context.GenericTokenPrincipal is null)
            return;

        var subValue = context.GenericTokenPrincipal.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
        var tidValue = context.GenericTokenPrincipal.FindFirst("tid")?.Value;

        if (!Guid.TryParse(subValue, out var userId) || !Guid.TryParse(tidValue, out var tenantId))
            return;

        var permissions = await permissionEvaluator.EvaluateAsync(userId, tenantId, context.CancellationToken);
        var dimensions = await dimensionEvaluator.EvaluateAsync(userId, tenantId, context.CancellationToken);

        var groups = await db.UserGroups
            .IgnoreQueryFilters()
            .Where(ug => ug.UserId == userId && ug.Group.TenantId == tenantId)
            .Select(ug => ug.Group.Name)
            .ToListAsync(context.CancellationToken);

        var roles = await db.UserGroups
            .IgnoreQueryFilters()
            .Where(ug => ug.UserId == userId && ug.Group.TenantId == tenantId)
            .SelectMany(ug => ug.Group.GroupRoles.Select(gr => gr.Role.Name))
            .Distinct()
            .ToListAsync(context.CancellationToken);

        OpenIddictServerHelpers.SetProperty(context.Transaction, PermissionsKey, permissions);
        OpenIddictServerHelpers.SetProperty(context.Transaction, DimensionsKey, dimensions);
        OpenIddictServerHelpers.SetProperty(context.Transaction, GroupsKey, groups);
        OpenIddictServerHelpers.SetProperty(context.Transaction, RolesKey, roles);
    }
}

// Stage 2: Runs during ApplyIntrospectionResponseContext after the response has been built.
// Writes enrichment data from the transaction directly to context.Response using AddParameter,
// bypassing OpenIddict's SetParameter which strips empty arrays (JsonArray with Count=0).
public sealed class IntrospectionResponseEnricher : IOpenIddictServerHandler<ApplyIntrospectionResponseContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<ApplyIntrospectionResponseContext>()
            .UseSingletonHandler<IntrospectionResponseEnricher>()
            .SetOrder(1_000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public ValueTask HandleAsync(ApplyIntrospectionResponseContext context)
    {
        var permissions = OpenIddictServerHelpers.GetProperty<IReadOnlySet<string>>(
            context.Transaction, IntrospectionDataEnricher.PermissionsKey);
        var dimensions = OpenIddictServerHelpers.GetProperty<IReadOnlyDictionary<string, IReadOnlyList<string>>>(
            context.Transaction, IntrospectionDataEnricher.DimensionsKey);
        var groups = OpenIddictServerHelpers.GetProperty<List<string>>(
            context.Transaction, IntrospectionDataEnricher.GroupsKey);
        var roles = OpenIddictServerHelpers.GetProperty<List<string>>(
            context.Transaction, IntrospectionDataEnricher.RolesKey);

        if (permissions is null || dimensions is null)
            return default;

        // Use JsonElement (not JsonNode) so empty arrays survive OpenIddictParameter's
        // internal conversion that strips JsonArray{Count=0} to ImmutableArray.Empty.
        var permissionsElement = JsonSerializer.SerializeToElement(permissions.ToArray());
        context.Response.AddParameter("permissions", new OpenIddictParameter(permissionsElement));

        // dimensional_attributes: JsonObject with all 5 axes, each an array.
        var dimensionsNode = new JsonObject();
        foreach (var pair in dimensions)
        {
            var arr = new JsonArray();
            foreach (var v in pair.Value)
                arr.Add(JsonValue.Create(v));
            dimensionsNode[pair.Key] = arr;
        }
        context.Response.SetParameter("dimensional_attributes", new OpenIddictParameter(dimensionsNode));

        var groupsElement = JsonSerializer.SerializeToElement((groups ?? []).ToArray());
        context.Response.AddParameter("groups", new OpenIddictParameter(groupsElement));

        var rolesElement = JsonSerializer.SerializeToElement((roles ?? []).ToArray());
        context.Response.AddParameter("od_roles", new OpenIddictParameter(rolesElement));

        // license: stub until Phase 6 stories 3-3/3-5 wire in real seat-count data.
        context.Response.SetParameter("license", new OpenIddictParameter(new JsonObject
        {
            ["status"] = "active",
            ["seats_used"] = 0,
            ["max_seats"] = 0
        }));

        return default;
    }
}
