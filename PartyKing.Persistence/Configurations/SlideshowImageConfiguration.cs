using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartyKing.Domain.Models.Slideshow;

namespace PartyKing.Persistence.Configurations;

public class SlideshowImageConfiguration : IEntityTypeConfiguration<SlideshowImage>
{
    public void Configure(EntityTypeBuilder<SlideshowImage> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ImageData);

        builder.Property(x => x.ImageUrl);

        builder.Property(x => x.ContentType);
    }
}