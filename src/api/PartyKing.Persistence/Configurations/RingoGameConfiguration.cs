using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartyKing.Domain.Entities.Games;
using PartyKing.Domain.Entities.Sessions;

namespace PartyKing.Persistence.Configurations;

public class RingoGameConfiguration : IEntityTypeConfiguration<RingoGame>
{
    public void Configure(EntityTypeBuilder<RingoGame> builder)
    {
        builder.HasKey(x => x.SessionId);
        builder.Property(x => x.SessionId)
            .ValueGeneratedNever();
        builder
            .HasOne<Session>()
            .WithOne()
            .HasForeignKey<RingoGame>(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasMany(x => x.Flips)
            .WithOne()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasMany(x => x.Players)
            .WithOne()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
