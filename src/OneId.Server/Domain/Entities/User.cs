namespace OneId.Server.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Email { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? PasswordHash { get; set; }
}
