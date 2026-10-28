using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartyKing.Domain.Entities.Sessions;

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
        builder
            .HasOne(x => x.HostProfile)
            .WithMany()
            .HasForeignKey(x => x.HostProfileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
