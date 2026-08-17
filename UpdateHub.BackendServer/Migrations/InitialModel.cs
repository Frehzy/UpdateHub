using Microsoft.EntityFrameworkCore;

#nullable disable

namespace UpdateHub.BackendServer.Migrations;

/// <summary>
/// Описание модели на момент миграции <see cref="Initial"/>.
/// </summary>
/// <remarks>
/// Обычно инструменты <c>dotnet ef</c> раскладывают одно и то же описание модели
/// по двум файлам: <c>*.Designer.cs</c> (состояние на момент миграции) и
/// <c>AppDbContextModelSnapshot.cs</c> (состояние после последней миграции).
/// Пока миграция одна, оба описания совпадают дословно, и держать две копии
/// семисот строк — значит гарантированно однажды поправить одну и забыть другую.
/// Расхождение при этом не заметит никто: тест сравнивает с моделью только снимок.
/// Поэтому описание лежит здесь в единственном экземпляре, а оба файла его
/// вызывают.
/// <para>
/// На работу инструментов это не влияет: <c>dotnet ef</c> читает собранную
/// сборку, а не исходный текст. Когда появится вторая миграция, её файлы
/// будут сгенерированы обычным образом и этот класс останется обслуживать
/// только первую.
/// </para>
/// </remarks>
internal static class InitialModel
{
    /// <summary>Строит модель в том виде, в каком её создаёт первая миграция.</summary>
    /// <param name="modelBuilder">Построитель модели.</param>
    public static void Build(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.11");

        BuildEntities(modelBuilder);
        BuildRelationships(modelBuilder);
        BuildNavigations(modelBuilder);
    }

    /// <summary>Описывает таблицы, столбцы, ключи и индексы.</summary>
    /// <param name="modelBuilder">Построитель модели.</param>
    private static void BuildEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.ClientBlockHistoryEntity", b =>
        {
            b.Property<string>("Id")
                .HasColumnType("TEXT");

            b.Property<string>("Action")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("BlockedBy")
                .HasColumnType("TEXT");

            b.Property<string>("ClientId")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<string>("Reason")
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("ClientId", "CreatedAt");

            b.ToTable("ClientBlockHistories");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.ClientComputerInfoEntity", b =>
        {
            b.Property<string>("Id")
                .HasColumnType("TEXT");

            b.Property<string>("Architecture")
                .HasColumnType("TEXT");

            b.Property<string>("ClientId")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("CpuInfo")
                .HasColumnType("TEXT");

            b.Property<int?>("DiskGb")
                .HasColumnType("INTEGER");

            b.Property<string>("HardwareFingerprint")
                .HasColumnType("TEXT");

            b.Property<string>("Hostname")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("KernelVersion")
                .HasColumnType("TEXT");

            b.Property<int?>("MemoryGb")
                .HasColumnType("INTEGER");

            b.Property<string>("OsVersion")
                .HasColumnType("TEXT");

            b.Property<DateTime>("UpdatedAt")
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("ClientId")
                .IsUnique();

            b.HasIndex("HardwareFingerprint");

            b.ToTable("ClientComputerInfos");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.ClientEntity", b =>
        {
            b.Property<string>("Id")
                .HasColumnType("TEXT");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<string>("GroupId")
                .HasColumnType("TEXT");

            b.Property<bool>("IsActive")
                .HasColumnType("INTEGER");

            b.Property<bool>("IsBlocked")
                .HasColumnType("INTEGER");

            b.Property<DateTime>("UpdatedAt")
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("GroupId");

            b.HasIndex("IsActive");

            b.ToTable("Clients");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.ClientHistoryEntity", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");

            b.Property<DateTime>("ChangeTimestamp")
                .HasColumnType("TEXT");

            b.Property<string>("ChangeType")
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnType("TEXT");

            b.Property<string>("ClientId")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("NewValue")
                .HasColumnType("TEXT");

            b.Property<string>("OldValue")
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("ClientId", "ChangeTimestamp");

            b.ToTable("ClientHistories");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.ClientNetworkInfoEntity", b =>
        {
            b.Property<string>("Id")
                .HasColumnType("TEXT");

            b.Property<string>("ClientId")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("IpAddress")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<bool>("IsActive")
                .HasColumnType("INTEGER");

            b.Property<DateTime>("LastSeen")
                .HasColumnType("TEXT");

            b.Property<string>("MacAddress")
                .HasColumnType("TEXT");

            b.Property<string>("NetworkInterface")
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("ClientId", "IpAddress")
                .IsUnique();

            b.ToTable("ClientNetworkInfos");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.EnrollmentRequestEntity", b =>
        {
            b.Property<string>("Id")
                .HasColumnType("TEXT");

            b.Property<string>("ClientId")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("Comment")
                .HasColumnType("TEXT");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<string>("HardwareFingerprint")
                .HasColumnType("TEXT");

            b.Property<string>("Hostname")
                .HasColumnType("TEXT");

            b.Property<string>("OsVersion")
                .HasColumnType("TEXT");

            b.Property<string>("RemoteIpAddress")
                .HasColumnType("TEXT");

            b.Property<string>("RequestedByUsername")
                .HasColumnType("TEXT");

            b.Property<DateTime?>("ResolvedAt")
                .HasColumnType("TEXT");

            b.Property<string>("ResolvedBy")
                .HasColumnType("TEXT");

            b.Property<string>("Status")
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("ClientId");

            b.HasIndex("Status");

            b.ToTable("EnrollmentRequests");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.FileChangeEntity", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");

            b.Property<DateTime>("ChangeTimestamp")
                .HasColumnType("TEXT");

            b.Property<string>("ChangeType")
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("TEXT");

            b.Property<string>("ManifestEntryId")
                .HasColumnType("TEXT");

            b.Property<string>("NewMd5Hash")
                .HasColumnType("TEXT");

            b.Property<string>("OldMd5Hash")
                .HasColumnType("TEXT");

            b.Property<string>("RelativePath")
                .IsRequired()
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("ChangeTimestamp");

            b.HasIndex("ManifestEntryId");

            b.ToTable("FileChanges");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.GroupEntity", b =>
        {
            b.Property<string>("Id")
                .HasColumnType("TEXT");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<string>("Description")
                .HasColumnType("TEXT");

            b.Property<bool>("IsActive")
                .HasColumnType("INTEGER");

            b.Property<string>("Name")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<DateTime>("UpdatedAt")
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("Name")
                .IsUnique();

            b.ToTable("Groups");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.ManifestEntryEntity", b =>
        {
            b.Property<string>("Id")
                .HasColumnType("TEXT");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<DateTime>("LastModified")
                .HasColumnType("TEXT");

            b.Property<string>("Md5Hash")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("RelativePath")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<long>("SizeBytes")
                .HasColumnType("INTEGER");

            b.Property<DateTime>("UpdatedAt")
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("RelativePath")
                .IsUnique();

            b.ToTable("ManifestEntries");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.RefreshTokenEntity", b =>
        {
            b.Property<string>("Id")
                .HasColumnType("TEXT");

            b.Property<string>("ClientIp")
                .HasColumnType("TEXT");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<DateTime>("ExpiresAt")
                .HasColumnType("TEXT");

            b.Property<DateTime?>("RevokedAt")
                .HasColumnType("TEXT");

            b.Property<string>("Token")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("UserAgent")
                .HasColumnType("TEXT");

            b.Property<string>("UserId")
                .IsRequired()
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("Token")
                .IsUnique();

            b.HasIndex("UserId");

            b.ToTable("RefreshTokens");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.UpdateDetailEntity", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");

            b.Property<string>("ManifestEntryId")
                .HasColumnType("TEXT");

            b.Property<string>("NewMd5Hash")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("OldMd5Hash")
                .HasColumnType("TEXT");

            b.Property<string>("RelativePath")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<long>("SizeBytes")
                .HasColumnType("INTEGER");

            b.Property<int>("UpdateRequestId")
                .HasColumnType("INTEGER");

            b.HasKey("Id");

            b.HasIndex("ManifestEntryId");

            b.HasIndex("UpdateRequestId");

            b.ToTable("UpdateDetails");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.UpdateRequestEntity", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");

            b.Property<string>("ClientId")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("ClientManifestHash")
                .HasColumnType("TEXT");

            b.Property<int>("FilesToUpdate")
                .HasColumnType("INTEGER");

            b.Property<DateTime>("RequestTimestamp")
                .HasColumnType("TEXT");

            b.Property<string>("RequestType")
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("TEXT");

            b.Property<int?>("ResponseTimeMs")
                .HasColumnType("INTEGER");

            b.Property<string>("Status")
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("TEXT");

            b.Property<long>("TotalSizeBytes")
                .HasColumnType("INTEGER");

            b.Property<string>("Username")
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("RequestTimestamp");

            b.HasIndex("ClientId", "RequestTimestamp");

            b.ToTable("UpdateRequests");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.UserClientAccessEntity", b =>
        {
            b.Property<string>("Id")
                .HasColumnType("TEXT");

            b.Property<string>("ClientId")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<string>("UserId")
                .IsRequired()
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("ClientId");

            b.HasIndex("UserId", "ClientId")
                .IsUnique();

            b.ToTable("UserClientAccesses");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.UserEntity", b =>
        {
            b.Property<string>("Id")
                .HasColumnType("TEXT");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<bool>("IsActive")
                .HasColumnType("INTEGER");

            b.Property<DateTime?>("LastLogin")
                .HasColumnType("TEXT");

            b.Property<bool>("MustChangePassword")
                .HasColumnType("INTEGER");

            b.Property<string>("PasswordHash")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("Role")
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("TEXT");

            b.Property<string>("Username")
                .IsRequired()
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("Username")
                .IsUnique();

            b.ToTable("Users");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.UserGroupAccessEntity", b =>
        {
            b.Property<string>("Id")
                .HasColumnType("TEXT");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<string>("GroupId")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("UserId")
                .IsRequired()
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("GroupId");

            b.HasIndex("UserId", "GroupId")
                .IsUnique();

            b.ToTable("UserGroupAccesses");
        });
    }

    /// <summary>Описывает внешние ключи и поведение при удалении.</summary>
    /// <param name="modelBuilder">Построитель модели.</param>
    private static void BuildRelationships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.ClientBlockHistoryEntity", b =>
        {
            b.HasOne("UpdateHub.BackendServer.Domain.Entities.ClientEntity", "Client")
                .WithMany("BlockHistory")
                .HasForeignKey("ClientId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Client");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.ClientComputerInfoEntity", b =>
        {
            b.HasOne("UpdateHub.BackendServer.Domain.Entities.ClientEntity", "Client")
                .WithOne("ComputerInfo")
                .HasForeignKey("UpdateHub.BackendServer.Domain.Entities.ClientComputerInfoEntity", "ClientId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Client");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.ClientEntity", b =>
        {
            b.HasOne("UpdateHub.BackendServer.Domain.Entities.GroupEntity", "Group")
                .WithMany("Clients")
                .HasForeignKey("GroupId")
                .OnDelete(DeleteBehavior.SetNull);

            b.Navigation("Group");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.ClientHistoryEntity", b =>
        {
            b.HasOne("UpdateHub.BackendServer.Domain.Entities.ClientEntity", "Client")
                .WithMany("History")
                .HasForeignKey("ClientId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Client");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.ClientNetworkInfoEntity", b =>
        {
            b.HasOne("UpdateHub.BackendServer.Domain.Entities.ClientEntity", "Client")
                .WithMany("NetworkInfos")
                .HasForeignKey("ClientId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Client");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.FileChangeEntity", b =>
        {
            b.HasOne("UpdateHub.BackendServer.Domain.Entities.ManifestEntryEntity", "ManifestEntry")
                .WithMany("FileChanges")
                .HasForeignKey("ManifestEntryId")
                .OnDelete(DeleteBehavior.SetNull);

            b.Navigation("ManifestEntry");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.RefreshTokenEntity", b =>
        {
            b.HasOne("UpdateHub.BackendServer.Domain.Entities.UserEntity", "User")
                .WithMany("RefreshTokens")
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("User");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.UpdateDetailEntity", b =>
        {
            b.HasOne("UpdateHub.BackendServer.Domain.Entities.ManifestEntryEntity", "ManifestEntry")
                .WithMany("UpdateDetails")
                .HasForeignKey("ManifestEntryId")
                .OnDelete(DeleteBehavior.SetNull);

            b.HasOne("UpdateHub.BackendServer.Domain.Entities.UpdateRequestEntity", "UpdateRequest")
                .WithMany("UpdateDetails")
                .HasForeignKey("UpdateRequestId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("ManifestEntry");

            b.Navigation("UpdateRequest");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.UpdateRequestEntity", b =>
        {
            b.HasOne("UpdateHub.BackendServer.Domain.Entities.ClientEntity", "Client")
                .WithMany("UpdateRequests")
                .HasForeignKey("ClientId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Client");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.UserClientAccessEntity", b =>
        {
            b.HasOne("UpdateHub.BackendServer.Domain.Entities.ClientEntity", "Client")
                .WithMany("UserClientAccesses")
                .HasForeignKey("ClientId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.HasOne("UpdateHub.BackendServer.Domain.Entities.UserEntity", "User")
                .WithMany("UserClientAccesses")
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Client");

            b.Navigation("User");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.UserGroupAccessEntity", b =>
        {
            b.HasOne("UpdateHub.BackendServer.Domain.Entities.GroupEntity", "Group")
                .WithMany("UserGroupAccesses")
                .HasForeignKey("GroupId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.HasOne("UpdateHub.BackendServer.Domain.Entities.UserEntity", "User")
                .WithMany("UserGroupAccesses")
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Group");

            b.Navigation("User");
        });
    }

    /// <summary>Объявляет навигационные свойства со стороны «одного ко многим».</summary>
    /// <param name="modelBuilder">Построитель модели.</param>
    private static void BuildNavigations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.ClientEntity", b =>
        {
            b.Navigation("BlockHistory");

            b.Navigation("ComputerInfo");

            b.Navigation("History");

            b.Navigation("NetworkInfos");

            b.Navigation("UpdateRequests");

            b.Navigation("UserClientAccesses");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.GroupEntity", b =>
        {
            b.Navigation("Clients");

            b.Navigation("UserGroupAccesses");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.ManifestEntryEntity", b =>
        {
            b.Navigation("FileChanges");

            b.Navigation("UpdateDetails");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.UpdateRequestEntity", b =>
        {
            b.Navigation("UpdateDetails");
        });

        modelBuilder.Entity("UpdateHub.BackendServer.Domain.Entities.UserEntity", b =>
        {
            b.Navigation("RefreshTokens");

            b.Navigation("UserClientAccesses");

            b.Navigation("UserGroupAccesses");
        });
    }
}
