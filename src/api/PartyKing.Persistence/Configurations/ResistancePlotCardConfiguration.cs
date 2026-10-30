using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartyKing.Domain.Entities.Games;

namespace PartyKing.Persistence.Configurations;

public class ResistancePlotCardConfiguration : IEntityTypeConfiguration<ResistancePlotCard>
{
    public void Configure(EntityTypeBuilder<ResistancePlotCard> builder)
    {
        builder.HasKey(x => new { x.SessionId, x.CardId });
        builder.Property(x => x.CardId)
            .ValueGeneratedNever();
    }
}
