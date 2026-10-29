using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartyKing.Domain.Entities.Games;

namespace PartyKing.Persistence.Configurations;

public class ResistancePlayerConfiguration : IEntityTypeConfiguration<ResistancePlayer>
{
    public void Configure(EntityTypeBuilder<ResistancePlayer> builder)
    {
        builder.HasKey(x => new { x.SessionId, x.PlayerId });
        builder.HasIndex(x => new { x.SessionId, x.NormalizedName })
            .IsUnique();
        builder.Property(x => x.Name)
            .HasMaxLength(100);
        builder.Property(x => x.NormalizedName)
            .HasMaxLength(100);
    }
}
