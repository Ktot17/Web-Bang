using BLComponent;
using Microsoft.EntityFrameworkCore;
using Server.Models;

namespace DBComponent.Postgres;

public class ServerDbContext : DbContext
{
    private const string TimeStamp = "timestamp";

    public ServerDbContext() { }
    public ServerDbContext(DbContextOptions<ServerDbContext> options) : base(options) { }

    public virtual DbSet<CardDb> Cards { get; set; } = null!;
    public virtual DbSet<User> Users { get; set; } = null!;
    public virtual DbSet<Game> Games { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema("server");

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever().HasColumnType("text");
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Email).IsRequired();
            entity.Property(e => e.Code);
            entity.Property(e => e.TwoFactorExpire).HasColumnType(TimeStamp);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.GameId).HasColumnType("text");
            entity.Property(e => e.FailedLoginCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.LockoutEnd).HasColumnType(TimeStamp);
            entity.Property(e => e.LastPasswordChange).IsRequired()
                .HasDefaultValue(DateTimeOffset.UtcNow.DateTime).HasColumnType(TimeStamp);
        });
        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever().HasColumnType("text");
            entity.Property(e => e.HostId).IsRequired().HasColumnType("text");
            entity.Property(e => e.GameState).HasColumnType("text");
            entity.Property(e => e.IsEnded).IsRequired();
            entity.Property(e => e.PlayerCount).IsRequired();
            entity.Property(e => e.LastUpdated).IsRequired().HasColumnType(TimeStamp);
        });

        modelBuilder.Entity<CardDb>(entity =>
        {
            entity.ToTable("classicdeck", "decks");
            entity.HasNoKey();
            entity.Property(e => e.Name);
            entity.Property(e => e.Suit);
            entity.Property(e => e.Rank);
        });
    }
}
