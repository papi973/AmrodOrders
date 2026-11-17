using Microsoft.EntityFrameworkCore;
using Orders.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Infrastructure.Persistence.Configurations
{
    public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> b)
        {
            b.ToTable("Customers", t => t.HasCheckConstraint("CK_Customers_CountryCode_Length", "LEN([CountryCode]) = 2"));

            b.HasKey(c => c.Id);
            b.Property(c => c.Id).ValueGeneratedNever(); // the domain assigns Ids

            b.Property(c => c.Name).HasMaxLength(200).IsRequired();
            b.Property(c => c.Email).HasMaxLength(320).IsRequired();
            b.Property(c => c.CountryCode).HasMaxLength(2).IsFixedLength().IsUnicode(false).IsRequired();
            b.Property(c => c.CreatedAt).IsRequired();

            b.HasIndex(c => c.Email).IsUnique().HasDatabaseName("UX_Customers_Email");
            b.HasIndex(c => c.Name).HasDatabaseName("IX_Customers_Name");
        }
    }
}
