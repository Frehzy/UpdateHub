using Microsoft.EntityFrameworkCore;
using UpdateHub.BackendServer.Domain.Entities;

namespace UpdateHub.BackendServer.Infrastructure.Database;

/// <summary>
/// Контекст базы данных SQLite.
/// </summary>
/// <remarks>
/// Файл базы обязан лежать на именованном томе Docker. Размещать его
/// в проброшенной с Windows папке нельзя: блокировки файлов через 9p/virtiofs
/// работают неправильно и приводят к ошибкам «database is locked» и порче базы.
/// </remarks>
/// <param name="options">Параметры контекста.</param>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>Учётные записи пользователей.</summary>
    public DbSet<UserEntity> Users => Set<UserEntity>();

    /// <summary>Выданные refresh-токены.</summary>
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

    /// <summary>Группы компьютеров.</summary>
    public DbSet<GroupEntity> Groups => Set<GroupEntity>();

    /// <summary>Компьютеры.</summary>
    public DbSet<ClientEntity> Clients => Set<ClientEntity>();

    /// <summary>Сведения о железе компьютеров.</summary>
    public DbSet<ClientComputerInfoEntity> ClientComputerInfos => Set<ClientComputerInfoEntity>();

    /// <summary>Сетевые адреса компьютеров.</summary>
    public DbSet<ClientNetworkInfoEntity> ClientNetworkInfos => Set<ClientNetworkInfoEntity>();

    /// <summary>История блокировок компьютеров.</summary>
    public DbSet<ClientBlockHistoryEntity> ClientBlockHistories => Set<ClientBlockHistoryEntity>();

    /// <summary>История изменений характеристик компьютеров.</summary>
    public DbSet<ClientHistoryEntity> ClientHistories => Set<ClientHistoryEntity>();

    /// <summary>Персональные разрешения на компьютеры.</summary>
    public DbSet<UserClientAccessEntity> UserClientAccesses => Set<UserClientAccessEntity>();

    /// <summary>Разрешения на группы компьютеров.</summary>
    public DbSet<UserGroupAccessEntity> UserGroupAccesses => Set<UserGroupAccessEntity>();

    /// <summary>Записи эталонного манифеста.</summary>
    public DbSet<ManifestEntryEntity> ManifestEntries => Set<ManifestEntryEntity>();

    /// <summary>Журнал обращений клиентов.</summary>
    public DbSet<UpdateRequestEntity> UpdateRequests => Set<UpdateRequestEntity>();

    /// <summary>Пофайловая детализация обращений.</summary>
    public DbSet<UpdateDetailEntity> UpdateDetails => Set<UpdateDetailEntity>();

    /// <summary>История изменений файлов каталога раздачи.</summary>
    public DbSet<FileChangeEntity> FileChanges => Set<FileChangeEntity>();

    /// <summary>Заявки на регистрацию компьютеров.</summary>
    public DbSet<EnrollmentRequestEntity> EnrollmentRequests => Set<EnrollmentRequestEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureEnumConversions(modelBuilder);
        ConfigureKeysAndIndexes(modelBuilder);
        ConfigureRelationships(modelBuilder);
    }

    /// <summary>
    /// Хранит перечисления строками, чтобы значения оставались читаемыми
    /// при прямом просмотре базы и не зависели от порядка объявления.
    /// </summary>
    /// <param name="modelBuilder">Построитель модели.</param>
    private static void ConfigureEnumConversions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>().Property(e => e.Role).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<ClientHistoryEntity>().Property(e => e.ChangeType).HasConversion<string>().HasMaxLength(64);
        modelBuilder.Entity<UpdateRequestEntity>().Property(e => e.RequestType).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<UpdateRequestEntity>().Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<FileChangeEntity>().Property(e => e.ChangeType).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<EnrollmentRequestEntity>().Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
    }

    /// <summary>Настраивает уникальные ограничения и индексы под используемые запросы.</summary>
    /// <param name="modelBuilder">Построитель модели.</param>
    private static void ConfigureKeysAndIndexes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>().HasIndex(e => e.Username).IsUnique();
        modelBuilder.Entity<GroupEntity>().HasIndex(e => e.Name).IsUnique();
        modelBuilder.Entity<ManifestEntryEntity>().HasIndex(e => e.RelativePath).IsUnique();

        modelBuilder.Entity<RefreshTokenEntity>().HasIndex(e => e.Token).IsUnique();
        modelBuilder.Entity<RefreshTokenEntity>().HasIndex(e => e.UserId);

        modelBuilder.Entity<UserClientAccessEntity>().HasIndex(e => new { e.UserId, e.ClientId }).IsUnique();
        modelBuilder.Entity<UserGroupAccessEntity>().HasIndex(e => new { e.UserId, e.GroupId }).IsUnique();

        modelBuilder.Entity<ClientComputerInfoEntity>().HasIndex(e => e.ClientId).IsUnique();
        modelBuilder.Entity<ClientComputerInfoEntity>().HasIndex(e => e.HardwareFingerprint);
        modelBuilder.Entity<ClientNetworkInfoEntity>().HasIndex(e => new { e.ClientId, e.IpAddress }).IsUnique();

        modelBuilder.Entity<ClientEntity>().HasIndex(e => e.IsActive);
        modelBuilder.Entity<ClientEntity>().HasIndex(e => e.GroupId);
        modelBuilder.Entity<ClientHistoryEntity>().HasIndex(e => new { e.ClientId, e.ChangeTimestamp });
        modelBuilder.Entity<ClientBlockHistoryEntity>().HasIndex(e => new { e.ClientId, e.CreatedAt });
        modelBuilder.Entity<UpdateRequestEntity>().HasIndex(e => e.RequestTimestamp);
        modelBuilder.Entity<UpdateRequestEntity>().HasIndex(e => new { e.ClientId, e.RequestTimestamp });
        modelBuilder.Entity<UpdateDetailEntity>().HasIndex(e => e.UpdateRequestId);
        modelBuilder.Entity<FileChangeEntity>().HasIndex(e => e.ChangeTimestamp);
        modelBuilder.Entity<EnrollmentRequestEntity>().HasIndex(e => e.Status);
        modelBuilder.Entity<EnrollmentRequestEntity>().HasIndex(e => e.ClientId);
    }

    /// <summary>Настраивает связи и поведение при удалении.</summary>
    /// <param name="modelBuilder">Построитель модели.</param>
    private static void ConfigureRelationships(ModelBuilder modelBuilder)
    {
        // Удаление группы не удаляет компьютеры — они просто остаются без группы.
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
            .HasOne(e => e.UpdateRequest)
            .WithMany(e => e.UpdateDetails)
            .HasForeignKey(e => e.UpdateRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Файл может исчезнуть из манифеста, а запись о выдаче должна пережить это.
        modelBuilder.Entity<UpdateDetailEntity>()
            .HasOne(e => e.ManifestEntry)
            .WithMany(e => e.UpdateDetails)
            .HasForeignKey(e => e.ManifestEntryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<FileChangeEntity>()
            .HasOne(e => e.ManifestEntry)
            .WithMany(e => e.FileChanges)
            .HasForeignKey(e => e.ManifestEntryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<RefreshTokenEntity>()
            .HasOne(e => e.User)
            .WithMany(e => e.RefreshTokens)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserClientAccessEntity>()
            .HasOne(e => e.User)
            .WithMany(e => e.UserClientAccesses)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserClientAccessEntity>()
            .HasOne(e => e.Client)
            .WithMany(e => e.UserClientAccesses)
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserGroupAccessEntity>()
            .HasOne(e => e.User)
            .WithMany(e => e.UserGroupAccesses)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserGroupAccessEntity>()
            .HasOne(e => e.Group)
            .WithMany(e => e.UserGroupAccesses)
            .HasForeignKey(e => e.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
