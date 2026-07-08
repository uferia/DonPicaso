using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Identity.Features.Brands;

namespace Modules.Identity.Features.Branches;

internal sealed class BranchEntityConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches");

        builder.HasKey(b => b.Id).HasName("pk_branches");

        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(b => b.BrandId).HasColumnName("brand_id").IsRequired();

        builder.Property(b => b.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(b => b.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(b => b.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasOne<Brand>()
            .WithMany()
            .HasForeignKey(b => b.BrandId)
            .HasConstraintName("fk_branches_brand_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.BrandId).HasDatabaseName("ix_branches_brand_id");
    }
}
