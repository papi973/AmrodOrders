using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Orders.Infrastructure.Persistence.Configurations
{
    internal sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
    {
        public void Configure(EntityTypeBuilder<ProcessedMessage> b)
        {
            b.ToTable("ProcessedMessages");
            b.HasKey(p => new { p.MessageId, p.Consumer }); // unique: a race between two workers fails safely
            b.Property(p => p.Consumer).HasMaxLength(100).IsUnicode(false);
        }
    }
}
