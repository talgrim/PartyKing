using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Entities.Spotify;

namespace PartyKing.Persistence.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).IsRequired();
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.PasswordHash).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();

        // One host account can own at most one session (NULLs are allowed to repeat).
        builder.HasIndex(x => x.HostProfileId).IsUnique();

        // Claim and release both change the host: a save based on a stale host must not overwrite the newer one.
        builder.Property(x => x.HostProfileId).IsConcurrencyToken();
        builder.Ignore(x => x.HostDisplayName);

        // The kind tells what FallbackPlaylistId names, and is null with it; the saved playlist's Guid view of it is not a column
        builder.Property(x => x.FallbackPlaylistKind).HasConversion<string>();
        builder.Ignore(x => x.FallbackSavedPlaylistId);
        builder
            .HasOne<SpotifyProfile>()
            .WithMany()
            .HasForeignKey(x => x.HostProfileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
