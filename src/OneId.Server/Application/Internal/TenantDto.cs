namespace OneId.Server.Application.Internal;

public sealed record TenantDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);

public sealed class NameTakenException : Exception
{
    public NameTakenException() : base("A tenant with this name already exists.") { }
}
