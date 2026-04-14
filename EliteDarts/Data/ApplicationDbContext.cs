using EliteDarts.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EliteDarts.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<Tournament> Tournaments => Set<Tournament>();
        public DbSet<Player> Players => Set<Player>();
        public DbSet<Entry> Entries => Set<Entry>();
        public DbSet<Board> Boards => Set<Board>();
        public DbSet<Match> Matches => Set<Match>();
        public DbSet<Score> Scores => Set<Score>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Entry>()
                .HasIndex(e => new { e.TournamentId, e.PlayerId })
                .IsUnique();

            builder.Entity<Board>()
                .HasIndex(b => new { b.TournamentId, b.BoardNumber })
                .IsUnique();

            builder.Entity<Match>()
                .HasOne(m => m.Player1)
                .WithMany()
                .HasForeignKey(m => m.Player1Id)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Match>()
                .HasOne(m => m.Player2)
                .WithMany()
                .HasForeignKey(m => m.Player2Id)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Match>()
                .HasOne(m => m.WinnerPlayer)
                .WithMany()
                .HasForeignKey(m => m.WinnerPlayerId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Score>()
                .HasOne(s => s.Player)
                .WithMany()
                .HasForeignKey(s => s.PlayerId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Match>()
                .HasOne(m => m.NextMatch)
                .WithMany()
                .HasForeignKey(m => m.NextMatchId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Tournament>()
                .HasOne(t => t.OwnerUser)
                .WithMany()
                .HasForeignKey(t => t.OwnerUserId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}