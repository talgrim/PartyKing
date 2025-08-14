using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities;
using PartyKing.Domain.Models.Slideshow;

namespace PartyKing.Persistence.Database;

public class ReaderDbContext : PartyKingDbContextBase
{
    public ReaderDbContext()
        : base(new DbContextOptions<PartyKingDbContextBase>())
    {
    }
}

public class WriterDbContext : PartyKingDbContextBase
{
    public WriterDbContext()
        : base(new DbContextOptions<PartyKingDbContextBase>())
    {
    }
}

public class PartyKingDbContextBase : DbContext
{
    protected PartyKingDbContextBase(DbContextOptions<PartyKingDbContextBase> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=PartyKing.db");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PartyKingDbContextBase).Assembly);
    }

    public DbSet<SlideshowImage> Images { get; set; } = null!;
    public DbSet<SlideshowSettings> SlideshowSettings { get; set; } = null!;
    public DbSet<SpotifyProfile> SpotifyProfiles { get; set; } = null!;
}