using BLComponent;
using Microsoft.EntityFrameworkCore;
using Server.Models;

namespace DBComponent.MySql;

public class ServerDbContext : DbContext
{
    private const string IdType = "char(36)";
    private const string DateType = "datetime";
    private const int MaxLength = 255;

    public ServerDbContext() { }
    public ServerDbContext(DbContextOptions<ServerDbContext> options) : base(options) { }

    public virtual DbSet<CardDb> Cards { get; set; } = null!;
    public virtual DbSet<User> Users { get; set; } = null!;
    public virtual DbSet<Game> Games { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType(IdType);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(MaxLength);

            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(MaxLength);

            entity.Property(e => e.Code)
                .HasMaxLength(MaxLength);

            entity.Property(e => e.TwoFactorExpire)
                .HasColumnType(DateType);

            entity.Property(e => e.PasswordHash)
                .IsRequired()
                .HasMaxLength(MaxLength);

            entity.Property(e => e.GameId)
                .HasColumnType(IdType);

            entity.Property(e => e.FailedLoginCount)
                .IsRequired()
                .HasDefaultValue(0);

            entity.Property(e => e.LockoutEnd)
                .HasColumnType(DateType);

            entity.Property(e => e.LastPasswordChange)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType(DateType);
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType(IdType);

            entity.Property(e => e.HostId)
                .IsRequired()
                .HasColumnType(IdType);

            entity.Property(e => e.GameState);

            entity.Property(e => e.IsEnded)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.PlayerCount)
                .IsRequired()
                .HasDefaultValue(0);

            entity.Property(e => e.LastUpdated)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType(DateType);
        });

        modelBuilder.Entity<CardDb>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.Name)
                .HasMaxLength(MaxLength);

            entity.Property(e => e.Suit)
                .HasMaxLength(MaxLength);

            entity.Property(e => e.Rank)
                .HasMaxLength(MaxLength);
        });
    }
}
