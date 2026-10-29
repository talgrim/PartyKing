using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Domain.Entities.Lyrics;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Entities.Slideshow;
using PartyKing.Domain.Entities.Spotify;

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
    /// <summary>Environment variable that overrides the SQLite file location (defaults to PartyKing.db in the working directory).</summary>
    public const string DatabasePathVariable = "PARTYKING_DATABASE_PATH";

    protected PartyKingDbContextBase(DbContextOptions<PartyKingDbContextBase> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={Environment.GetEnvironmentVariable(DatabasePathVariable) ?? "PartyKing.db"}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PartyKingDbContextBase).Assembly);
    }

    public DbSet<SlideshowImage> Images { get; set; } = null!;
    public DbSet<SlideshowSettings> SlideshowSettings { get; set; } = null!;
    public DbSet<SpotifyProfile> SpotifyProfiles { get; set; } = null!;
    public DbSet<QueueItem> QueueItems { get; set; } = null!;
    public DbSet<Session> Sessions { get; set; } = null!;
    public DbSet<AdminAccount> AdminAccounts { get; set; } = null!;
    public DbSet<OAuthState> OAuthStates { get; set; } = null!;
    public DbSet<LyricsOffset> LyricsOffsets { get; set; } = null!;
}