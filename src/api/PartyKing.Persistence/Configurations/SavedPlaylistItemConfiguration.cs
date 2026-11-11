using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartyKing.Domain.Entities.Playlists;

namespace PartyKing.Persistence.Configurations;

public class SavedPlaylistItemConfiguration : IEntityTypeConfiguration<SavedPlaylistItem>
{
    public void Configure(EntityTypeBuilder<SavedPlaylistItem> builder)
    {
        builder.ToTable("SavedPlaylistItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Source).HasConversion<string>();
        builder.Property(x => x.Url).IsRequired();
        builder.Property(x => x.Title).IsRequired();
        builder.HasIndex(x => new { x.PlaylistId, x.Position });
    }
}
