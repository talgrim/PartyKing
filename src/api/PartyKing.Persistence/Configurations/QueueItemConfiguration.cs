using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartyKing.Domain.Entities;

namespace PartyKing.Persistence.Configurations;

public class QueueItemConfiguration : IEntityTypeConfiguration<QueueItem>
{
    public void Configure(EntityTypeBuilder<QueueItem> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Source).HasConversion<string>();
        builder.Property(x => x.Status).HasConversion<string>();
        builder.Property(x => x.Url).IsRequired();
        builder.Property(x => x.Title).IsRequired();
        builder.HasIndex(x => x.Status);
    }
}
