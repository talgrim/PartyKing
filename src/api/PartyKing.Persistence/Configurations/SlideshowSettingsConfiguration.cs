using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartyKing.Domain.Entities;

namespace PartyKing.Persistence.Configurations;

public class SlideshowSettingsConfiguration : IEntityTypeConfiguration<SlideshowSettings>
{
    public void Configure(EntityTypeBuilder<SlideshowSettings> builder)
    {
        builder.HasKey(x => x.SessionId);
        builder.Property(x => x.SessionId)
            .ValueGeneratedNever();
        builder
            .HasOne<Session>()
            .WithOne()
            .HasForeignKey<SlideshowSettings>(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
