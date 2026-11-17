using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Orders.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> b)
    {
        b.ToTable("IdempotencyRecords");
        b.HasKey(r => r.Key);
        b.Property(r => r.Key).HasMaxLength(150).IsUnicode(false);
        b.Property(r => r.RequestHash).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
        b.Property(r => r.CreatedAt).IsRequired();

        b.HasIndex(r => r.CreatedAt).HasDatabaseName("IX_IdempotencyRecords_CreatedAt"); 
    }
}