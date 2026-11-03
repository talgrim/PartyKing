using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Persistence.Configurations;

public class ResistanceMissionResultConfiguration : IEntityTypeConfiguration<ResistanceMissionResult>
{
    public void Configure(EntityTypeBuilder<ResistanceMissionResult> builder)
    {
        builder.HasKey(x => new { x.SessionId, x.MissionNumber });
    }
}
