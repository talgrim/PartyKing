using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartyKing.Domain.Entities.Lyrics;

namespace PartyKing.Persistence.Configurations;

public class LyricsOffsetConfiguration : IEntityTypeConfiguration<LyricsOffset>
{
    public void Configure(EntityTypeBuilder<LyricsOffset> builder)
    {
        builder.HasKey(x => x.SongUrl);
        builder.Property(x => x.SongUrl)
            .ValueGeneratedNever();
    }
}
