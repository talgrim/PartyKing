using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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

        // The owner is an account of the identity service, so there is no foreign key: the playlists go away when its
        // identity service reports the account as deleted (an `account.deleted` event)
        builder
            .HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
