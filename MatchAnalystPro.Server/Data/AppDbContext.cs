using MatchAnalystPro.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MatchAnalystPro.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Possession> Possessions => Set<Possession>();
    public DbSet<PitchCalibration> PitchCalibrations => Set<PitchCalibration>();
    public DbSet<OppositionReport> OppositionReports => Set<OppositionReport>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Event>()
          .HasIndex(e => new { e.MatchId, e.FrameNumber });

        mb.Entity<Event>()
          .HasIndex(e => new { e.MatchId, e.Category, e.SubType });

        mb.Entity<Event>()
          .HasIndex(e => new { e.PlayerId, e.Category });

        mb.Entity<Event>()
          .HasOne(e => e.Possession)
          .WithMany(p => p.Events)
          .HasForeignKey(e => e.PossessionId)
          .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<Match>()
          .HasOne(m => m.HomeTeam).WithMany()
          .HasForeignKey(m => m.HomeTeamId).OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Match>()
          .HasOne(m => m.AwayTeam).WithMany()
          .HasForeignKey(m => m.AwayTeamId).OnDelete(DeleteBehavior.Restrict);
    }
}
