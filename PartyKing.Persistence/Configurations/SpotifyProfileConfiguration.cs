using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartyKing.Domain.Entities;

namespace PartyKing.Persistence.Configurations;

public class SpotifyProfileConfiguration : IEntityTypeConfiguration<SpotifyProfile>
{
    public void Configure(EntityTypeBuilder<SpotifyProfile> builder)
    {
        builder
            .Property(x => x.Id)
            .ValueGeneratedNever();

        builder.HasKey(x => x.Id);

        builder
            .HasIndex(x => x.RefreshToken)
            .IsUnique();
    }
}