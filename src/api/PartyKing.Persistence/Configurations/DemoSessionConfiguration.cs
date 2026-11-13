using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartyKing.Domain.Entities.Demo;
using PartyKing.Domain.Entities.Sessions;

namespace PartyKing.Persistence.Configurations;

public class DemoSessionConfiguration : IEntityTypeConfiguration<DemoSession>
{
    public void Configure(EntityTypeBuilder<DemoSession> builder)
    {
        builder.ToTable("DemoSessions");
        builder.HasKey(x => x.SessionId);
        builder.Property(x => x.SessionId).ValueGeneratedNever();
        builder.Property(x => x.HostProfileId).IsRequired();
        builder.HasIndex(x => x.ExpiresAt);
        builder
            .HasOne<Session>()
            .WithMany()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
