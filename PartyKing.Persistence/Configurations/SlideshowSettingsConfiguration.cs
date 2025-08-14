using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartyKing.Domain.Entities;

namespace PartyKing.Persistence.Configurations;

public class SlideshowSettingsConfiguration : IEntityTypeConfiguration<SlideshowSettings>
{
    public void Configure(EntityTypeBuilder<SlideshowSettings> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.HasData(new SlideshowSettings
        {
            AutoRepeat = false,
            StartFromBeginning = false,
            SlideTime = TimeSpan.FromSeconds(20)
        });
    }
}