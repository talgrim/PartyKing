using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartyKing.Domain.Entities.Games.Resistance;
using PartyKing.Domain.Entities.Sessions;

namespace PartyKing.Persistence.Configurations;

public class ResistanceGameConfiguration : IEntityTypeConfiguration<ResistanceGame>
{
    public void Configure(EntityTypeBuilder<ResistanceGame> builder)
    {
        builder.HasKey(x => x.SessionId);
        builder.Property(x => x.SessionId)
            .ValueGeneratedNever();
        builder.Property(x => x.Rules)
            .HasConversion(
                x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                x => JsonSerializer.Deserialize<ResistanceRules>(x, (JsonSerializerOptions?)null)!)
            .HasDefaultValue(ResistanceRules.Default());
        // The state objects are columns of ResistanceGames: renaming a column name below needs a migration
        // (ResistanceGameColumnsTests pins them).
        builder.ComplexProperty(x => x.Inquisition, inquisition =>
        {
            inquisition.Property(x => x.HolderPlayerId).HasColumnName("InquisitorPlayerId");
            inquisition.Property(x => x.ExaminerPlayerId).HasColumnName("InquisitionExaminerPlayerId");
        });
        builder.ComplexProperty(
            x => x.DefectorDeck,
            deck => deck.Property(x => x.Cards).HasColumnName("DefectorDeck"));
        builder
            .HasOne<Session>()
            .WithOne()
            .HasForeignKey<ResistanceGame>(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasMany(x => x.Players)
            .WithOne()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasMany(x => x.MissionResults)
            .WithOne()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasMany(x => x.PlotCards)
            .WithOne()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
