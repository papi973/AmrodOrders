using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Infrastructure.Persistence.Configurations
{
    internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> b)
        {
            b.ToTable("OutboxMessages");
            b.HasKey(m => m.Id);
            b.Property(m => m.Id).ValueGeneratedNever();
            b.Property(x => x.Type).IsRequired().HasMaxLength(200);
            b.Property(m => m.RoutingKey).HasMaxLength(200).IsUnicode(false).IsRequired();
            b.Property(m => m.Payload).IsRequired();
            b.Property(m => m.CorrelationId).HasMaxLength(64).IsUnicode(false);
            b.Property(m => m.LastError).HasMaxLength(2000);

            // Small, fast index: only rows still waiting to be published.
            b.HasIndex(m => m.OccurredAt)
             .HasDatabaseName("IX_OutboxMessages_Unprocessed")
             .HasFilter("[ProcessedAt] IS NULL");
        }
    }
}
