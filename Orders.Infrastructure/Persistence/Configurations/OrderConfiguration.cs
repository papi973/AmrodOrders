using System;
using System.Collections.Generic;
using System.Text;
using Orders.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Orders.Infrastructure.Persistence.Configurations
{
    internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> b)
        {
            b.ToTable("Orders", t =>
            {
                t.HasCheckConstraint("CK_Orders_TotalAmount_NonNegative", "[TotalAmount] >= 0");
                t.HasCheckConstraint("CK_Orders_Status",
                    "[Status] IN ('Pending', 'Paid', 'Fulfilled', 'Cancelled')");
            });

            b.HasKey(o => o.Id);
            b.Property(o => o.Id).ValueGeneratedNever();

            b.Property(o => o.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired();
            b.Property(o => o.CurrencyCode).HasMaxLength(3).IsFixedLength().IsUnicode(false).IsRequired();
            b.Property(o => o.TotalAmount).HasPrecision(18, 2);
            b.Property(o => o.CreatedAt).IsRequired();
            b.Property(o => o.RowVersion).IsRowVersion();

            b.HasOne(o => o.Customer)
             .WithMany()
             .HasForeignKey(o => o.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasMany(o => o.LineItems)
             .WithOne()
             .HasForeignKey(li => li.OrderId)
             .OnDelete(DeleteBehavior.Cascade);

            b.Navigation(o => o.LineItems)
             .HasField("_lineItems")
             .UsePropertyAccessMode(PropertyAccessMode.Field);

            b.HasIndex(o => new { o.CustomerId, o.Status, o.CreatedAt })
             .HasDatabaseName("IX_Orders_CustomerId_Status_CreatedAt")
             .IncludeProperties(o => new { o.TotalAmount, o.CurrencyCode });

            b.HasIndex(o => o.CreatedAt).HasDatabaseName("IX_Orders_CreatedAt");
        }
    }
}
