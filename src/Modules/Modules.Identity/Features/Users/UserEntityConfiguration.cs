using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Identity.Features.Branches;
using Modules.Identity.Features.Brands;

namespace Modules.Identity.Features.Users;

internal sealed class UserEntityConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id).HasName("pk_users");

        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(320);

        builder.Property(u => u.PasswordHash).HasColumnName("password_hash");

        builder.Property(u => u.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.PinHash).HasColumnName("pin_hash");

        builder.Property(u => u.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(u => u.BrandId).HasColumnName("brand_id");
        builder.Property(u => u.BranchId).HasColumnName("branch_id");

        builder.Property(u => u.IsActive).HasColumnName("is_active").IsRequired().HasDefaultValue(true);

        builder.Property(u => u.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasOne<Brand>()
            .WithMany()
            .HasForeignKey(u => u.BrandId)
            .HasConstraintName("fk_users_brand_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(u => u.BranchId)
            .HasConstraintName("fk_users_branch_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("email IS NOT NULL")
            .HasDatabaseName("ux_users_email");

        builder.HasIndex(u => u.BrandId).HasDatabaseName("ix_users_brand_id");
        builder.HasIndex(u => u.BranchId).HasDatabaseName("ix_users_branch_id");
    }
}
