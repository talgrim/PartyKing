using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Domain.Entities.Playlists;

namespace PartyKing.Persistence.Configurations;

public class SavedPlaylistConfiguration : IEntityTypeConfiguration<SavedPlaylist>
{
    public void Configure(EntityTypeBuilder<SavedPlaylist> builder)
    {
        builder.ToTable("SavedPlaylists");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(SavedPlaylist.MaxNameLength);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => x.OwnerAccountId);

        // The playlist goes away with its owner
        builder
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(x => x.OwnerAccountId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
