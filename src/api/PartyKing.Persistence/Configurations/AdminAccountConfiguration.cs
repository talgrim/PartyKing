using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartyKing.Domain.Entities;

namespace PartyKing.Persistence.Configurations;

public class AdminAccountConfiguration : IEntityTypeConfiguration<AdminAccount>
{
    public void Configure(EntityTypeBuilder<AdminAccount> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Username).IsRequired();
        builder.Property(x => x.PasswordHash).IsRequired();
        builder.HasIndex(x => x.Username).IsUnique();
    }
}
