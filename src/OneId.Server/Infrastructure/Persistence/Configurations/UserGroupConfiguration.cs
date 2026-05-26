using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence.Configurations;

public class UserGroupConfiguration : IEntityTypeConfiguration<UserGroup>
{
    public void Configure(EntityTypeBuilder<UserGroup> builder)
    {
        builder.HasKey(ug => new { ug.GroupId, ug.UserId });
        builder.HasOne(ug => ug.Group).WithMany(g => g.UserGroups).HasForeignKey(ug => ug.GroupId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(ug => ug.User).WithMany().HasForeignKey(ug => ug.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
