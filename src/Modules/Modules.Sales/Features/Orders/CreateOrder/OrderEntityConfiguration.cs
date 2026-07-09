using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Modules.Sales.Features.Orders.CreateOrder;

/// <summary>
/// PostgreSQL mapping for the schema introduced by this slice, using explicit
/// snake_case names so the database convention never depends on a global
/// naming plugin.
/// </summary>
internal sealed class OrderEntityConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(o => o.Id).HasName("pk_orders");

        builder.Property(o => o.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(o => o.ClientOrderId)
            .HasColumnName("client_order_id")
            .IsRequired();

        builder.Property(o => o.BranchId)
            .HasColumnName("branch_id")
            .IsRequired();

        builder.Property(o => o.BrandId)
            .HasColumnName("brand_id")
            .IsRequired();

        builder.Property(o => o.TotalAmount)
            .HasColumnName("total_amount")
            .HasColumnType("numeric(12,2)")
            .IsRequired();

        builder.Property(o => o.Subtotal)
            .HasColumnName("subtotal")
            .HasColumnType("numeric(12,2)")
            .IsRequired();

        builder.Property(o => o.DiscountPercent)
            .HasColumnName("discount_percent")
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(o => o.DiscountAmount)
            .HasColumnName("discount_amount")
            .HasColumnType("numeric(12,2)")
            .IsRequired();

        builder.Property(o => o.TaxRatePercent)
            .HasColumnName("tax_rate_percent")
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(o => o.TaxAmount)
            .HasColumnName("tax_amount")
            .HasColumnType("numeric(12,2)")
            .IsRequired();

        builder.Property(o => o.PaymentMethod)
            .HasColumnName("payment_method")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.CashTendered)
            .HasColumnName("cash_tendered")
            .HasColumnType("numeric(12,2)");

        builder.Property(o => o.ChangeDue)
            .HasColumnName("change_due")
            .HasColumnType("numeric(12,2)");

        builder.Property(o => o.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .HasConstraintName("fk_order_items_order_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(o => o.ClientOrderId)
            .IsUnique()
            .HasDatabaseName("ux_orders_client_order_id");

        builder.HasIndex(o => o.BranchId).HasDatabaseName("ix_orders_branch_id");
        builder.HasIndex(o => o.BrandId).HasDatabaseName("ix_orders_brand_id");
    }
}

internal sealed class OrderItemEntityConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");

        builder.HasKey(i => i.Id).HasName("pk_order_items");

        builder.Property(i => i.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(i => i.OrderId)
            .HasColumnName("order_id")
            .IsRequired();

        builder.Property(i => i.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        builder.Property(i => i.ProductName)
            .HasColumnName("product_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.Quantity)
            .HasColumnName("quantity")
            .IsRequired();

        builder.Property(i => i.UnitPrice)
            .HasColumnName("unit_price")
            .HasColumnType("numeric(12,2)")
            .IsRequired();

        builder.Ignore(i => i.LineTotal);

        builder.HasIndex(i => i.OrderId).HasDatabaseName("ix_order_items_order_id");
    }
}
