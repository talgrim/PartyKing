using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartyKing.Domain.Entities.Games;

namespace PartyKing.Persistence.Configurations;

public class RingoFlipConfiguration : IEntityTypeConfiguration<RingoFlip>
{
    public void Configure(EntityTypeBuilder<RingoFlip> builder)
    {
        builder.HasKey(x => new { x.SessionId, x.Order });
        builder.HasIndex(x => new { x.SessionId, x.CardId })
            .IsUnique();
        builder.Property(x => x.FlippedBy)
            .HasMaxLength(100);
    }
}
