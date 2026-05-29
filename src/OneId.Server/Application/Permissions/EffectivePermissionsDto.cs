namespace OneId.Server.Application.Permissions;

public sealed record ProvenanceNodeDto(string NodeType, string Id, string Label, string Href);

public sealed record PermissionEntryDto(
    string Id,
    string Label,
    bool IsDenied,
    List<ProvenanceNodeDto> ProvenanceChain);

public sealed record EffectivePermissionsResponse(
    string UserId,
    string ResolvedAt,
    bool HasGroupAssignments,
    List<PermissionEntryDto> Permissions);
