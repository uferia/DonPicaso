using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Modules.Menu.Features.Catalog;

internal sealed class ProductEntityConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(p => p.Id).HasName("pk_products");

        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.CategoryId).HasColumnName("category_id").IsRequired();
        builder.Property(p => p.BrandId).HasColumnName("brand_id").IsRequired();
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(p => p.Price).HasColumnName("price").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(p => p.ImageUrl).HasColumnName("image_url").HasMaxLength(2000);
        builder.Property(p => p.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.Property(p => p.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .HasConstraintName("fk_products_category_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.CategoryId).HasDatabaseName("ix_products_category_id");
        builder.HasIndex(p => p.BrandId).HasDatabaseName("ix_products_brand_id");
    }
}
