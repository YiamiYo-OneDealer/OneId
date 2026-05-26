using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence.Configurations;

public class RoleSetRoleConfiguration : IEntityTypeConfiguration<RoleSetRole>
{
    public void Configure(EntityTypeBuilder<RoleSetRole> builder)
    {
        builder.HasKey(rsr => new { rsr.RoleSetId, rsr.RoleId });
    }
}
