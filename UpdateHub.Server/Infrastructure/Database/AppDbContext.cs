using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Infrastructure.Database;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users { get; set; }
    public DbSet<RefreshTokenEntity> RefreshTokens { get; set; }
    public DbSet<GroupEntity> Groups { get; set; }
    public DbSet<ClientEntity> Clients { get; set; }
    public DbSet<ClientComputerInfoEntity> ClientComputerInfos { get; set; }
    public DbSet<ClientNetworkInfoEntity> ClientNetworkInfos { get; set; }
    public DbSet<ClientSessionEntity> ClientSessions { get; set; }
    public DbSet<ClientBlockHistoryEntity> ClientBlockHistories { get; set; }
    public DbSet<ClientHistoryEntity> ClientHistories { get; set; }
    public DbSet<UserClientAccessEntity> UserClientAccesses { get; set; }
    public DbSet<UserGroupAccessEntity> UserGroupAccesses { get; set; }
    public DbSet<ManifestEntryEntity> ManifestEntries { get; set; }
    public DbSet<UpdateRequestEntity> UpdateRequests { get; set; }
    public DbSet<UpdateDetailEntity> UpdateDetails { get; set; }
    public DbSet<FileChangeEntity> FileChanges { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Конвертация enum в string
        modelBuilder.Entity<UserEntity>()
            .Property(e => e.Role)
            .HasConversion<string>();

        modelBuilder.Entity<ClientHistoryEntity>()
            .Property(e => e.ChangeType)
            .HasConversion<string>();

        modelBuilder.Entity<UpdateRequestEntity>()
            .Property(e => e.RequestType)
            .HasConversion<string>();

        modelBuilder.Entity<UpdateRequestEntity>()
            .Property(e => e.Status)
            .HasConversion<string>();

        modelBuilder.Entity<FileChangeEntity>()
            .Property(e => e.ChangeType)
            .HasConversion<string>();

        // Уникальные индексы
        modelBuilder.Entity<UserEntity>()
            .HasIndex(e => e.Username)
            .IsUnique();

        modelBuilder.Entity<GroupEntity>()
            .HasIndex(e => e.Name)
            .IsUnique();

        modelBuilder.Entity<ManifestEntryEntity>()
            .HasIndex(e => e.RelativePath)
            .IsUnique();

        // Составные уникальные индексы
        modelBuilder.Entity<UserClientAccessEntity>()
            .HasIndex(e => new { e.UserId, e.ClientId })
            .IsUnique();

        modelBuilder.Entity<UserGroupAccessEntity>()
            .HasIndex(e => new { e.UserId, e.GroupId })
            .IsUnique();

        // Внешние ключи с каскадным удалением
        modelBuilder.Entity<ClientEntity>()
            .HasOne(e => e.Group)
            .WithMany(e => e.Clients)
            .HasForeignKey(e => e.GroupId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ClientComputerInfoEntity>()
            .HasOne(e => e.Client)
            .WithOne(e => e.ComputerInfo)
            .HasForeignKey<ClientComputerInfoEntity>(e => e.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ClientNetworkInfoEntity>()
            .HasOne(e => e.Client)
            .WithMany(e => e.NetworkInfos)
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ClientSessionEntity>()
            .HasOne(e => e.Client)
            .WithMany(e => e.Sessions)
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ClientBlockHistoryEntity>()
            .HasOne(e => e.Client)
            .WithMany(e => e.BlockHistory)
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ClientHistoryEntity>()
            .HasOne(e => e.Client)
            .WithMany(e => e.History)
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UpdateRequestEntity>()
            .HasOne(e => e.Client)
            .WithMany(e => e.UpdateRequests)
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UpdateDetailEntity>()
            .HasOne(e => e.ManifestEntry)
            .WithMany(e => e.UpdateDetails)
            .HasForeignKey(e => e.ManifestEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FileChangeEntity>()
            .HasOne(e => e.ManifestEntry)
            .WithMany(e => e.FileChanges)
            .HasForeignKey(e => e.ManifestEntryId)
            .OnDelete(DeleteBehavior.SetNull);

        // Индексы для производительности
        modelBuilder.Entity<ClientEntity>().HasIndex(e => e.IsActive);
        modelBuilder.Entity<ClientEntity>().HasIndex(e => e.IsBlocked);
        modelBuilder.Entity<ClientNetworkInfoEntity>().HasIndex(e => e.IpAddress);
        modelBuilder.Entity<ClientNetworkInfoEntity>().HasIndex(e => e.LastSeen);
        modelBuilder.Entity<ClientSessionEntity>().HasIndex(e => e.IsActive);
        modelBuilder.Entity<UpdateRequestEntity>().HasIndex(e => e.RequestTimestamp);
        modelBuilder.Entity<FileChangeEntity>().HasIndex(e => e.IsProcessed);
    }
}