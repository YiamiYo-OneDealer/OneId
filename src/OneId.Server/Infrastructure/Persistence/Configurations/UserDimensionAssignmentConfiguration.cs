using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence.Configurations;

public class UserDimensionAssignmentConfiguration : IEntityTypeConfiguration<UserDimensionAssignment>
{
    public void Configure(EntityTypeBuilder<UserDimensionAssignment> builder)
    {
        builder.ToTable("user_dimension_assignments");
        builder.HasKey(a => a.Id);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.DimensionValue)
            .WithMany()
            .HasForeignKey(a => a.DimensionValueId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.UserId, a.DimensionValueId }).IsUnique();

        // AR-14: manual xmin shadow property (Npgsql v10)
        builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        // DO NOT add HasQueryFilter here — global filter is set in AppDbContext.OnModelCreating
    }
}
