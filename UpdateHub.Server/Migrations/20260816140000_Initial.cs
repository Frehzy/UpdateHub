using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UpdateHub.Server.Migrations;

/// <summary>
/// Первая миграция: создаёт всю схему базы данных с нуля.
/// </summary>
/// <remarks>
/// До появления миграций схема создавалась вызовом <c>EnsureCreated</c>. Он
/// удобен ровно один раз: строит базу по текущей модели и больше никогда её
/// не трогает. Любое последующее изменение сущности требовало бы удаления
/// файла базы вместе с учётными записями и историей обращений, а на сервере
/// без интернета это отдельная поездка. Миграции снимают эту проблему:
/// <see cref="Infrastructure.Database.DatabaseInitializer"/> сам переключается
/// на <c>Migrate</c>, как только в сборке появляется хотя бы одна миграция.
/// <para>
/// Таблицы создаются в порядке зависимостей: сначала те, на которые ссылаются,
/// затем ссылающиеся. SQLite проверяет внешние ключи при создании таблицы
/// не всегда, но полагаться на это не стоит.
/// </para>
/// </remarks>
public partial class Initial : Migration
{
    /// <summary>Создаёт таблицы и индексы.</summary>
    /// <param name="migrationBuilder">Построитель операций миграции.</param>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EnrollmentRequests",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                ClientId = table.Column<string>(type: "TEXT", nullable: false),
                Comment = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                HardwareFingerprint = table.Column<string>(type: "TEXT", nullable: true),
                Hostname = table.Column<string>(type: "TEXT", nullable: true),
                OsVersion = table.Column<string>(type: "TEXT", nullable: true),
                RemoteIpAddress = table.Column<string>(type: "TEXT", nullable: true),
                RequestedByUsername = table.Column<string>(type: "TEXT", nullable: true),
                ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                ResolvedBy = table.Column<string>(type: "TEXT", nullable: true),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EnrollmentRequests", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Groups",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: true),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Groups", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ManifestEntries",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                Md5Hash = table.Column<string>(type: "TEXT", nullable: false),
                RelativePath = table.Column<string>(type: "TEXT", nullable: false),
                SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ManifestEntries", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                LastLogin = table.Column<DateTime>(type: "TEXT", nullable: true),
                MustChangePassword = table.Column<bool>(type: "INTEGER", nullable: false),
                PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                Role = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Username = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Clients",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                GroupId = table.Column<string>(type: "TEXT", nullable: true),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                IsBlocked = table.Column<bool>(type: "INTEGER", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Clients", x => x.Id);
                table.ForeignKey(
                    name: "FK_Clients_Groups_GroupId",
                    column: x => x.GroupId,
                    principalTable: "Groups",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "FileChanges",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ChangeTimestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                ChangeType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                ManifestEntryId = table.Column<string>(type: "TEXT", nullable: true),
                NewMd5Hash = table.Column<string>(type: "TEXT", nullable: true),
                OldMd5Hash = table.Column<string>(type: "TEXT", nullable: true),
                RelativePath = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FileChanges", x => x.Id);
                table.ForeignKey(
                    name: "FK_FileChanges_ManifestEntries_ManifestEntryId",
                    column: x => x.ManifestEntryId,
                    principalTable: "ManifestEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "RefreshTokens",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                ClientIp = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                Token = table.Column<string>(type: "TEXT", nullable: false),
                UserAgent = table.Column<string>(type: "TEXT", nullable: true),
                UserId = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                table.ForeignKey(
                    name: "FK_RefreshTokens_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ClientBlockHistories",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Action = table.Column<string>(type: "TEXT", nullable: false),
                BlockedBy = table.Column<string>(type: "TEXT", nullable: true),
                ClientId = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                Reason = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClientBlockHistories", x => x.Id);
                table.ForeignKey(
                    name: "FK_ClientBlockHistories_Clients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ClientComputerInfos",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Architecture = table.Column<string>(type: "TEXT", nullable: true),
                ClientId = table.Column<string>(type: "TEXT", nullable: false),
                CpuInfo = table.Column<string>(type: "TEXT", nullable: true),
                DiskGb = table.Column<int>(type: "INTEGER", nullable: true),
                HardwareFingerprint = table.Column<string>(type: "TEXT", nullable: true),
                Hostname = table.Column<string>(type: "TEXT", nullable: false),
                KernelVersion = table.Column<string>(type: "TEXT", nullable: true),
                MemoryGb = table.Column<int>(type: "INTEGER", nullable: true),
                OsVersion = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClientComputerInfos", x => x.Id);
                table.ForeignKey(
                    name: "FK_ClientComputerInfos_Clients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ClientHistories",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ChangeTimestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                ChangeType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ClientId = table.Column<string>(type: "TEXT", nullable: false),
                NewValue = table.Column<string>(type: "TEXT", nullable: true),
                OldValue = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClientHistories", x => x.Id);
                table.ForeignKey(
                    name: "FK_ClientHistories_Clients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ClientNetworkInfos",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                ClientId = table.Column<string>(type: "TEXT", nullable: false),
                IpAddress = table.Column<string>(type: "TEXT", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                LastSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                MacAddress = table.Column<string>(type: "TEXT", nullable: true),
                NetworkInterface = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClientNetworkInfos", x => x.Id);
                table.ForeignKey(
                    name: "FK_ClientNetworkInfos_Clients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UpdateRequests",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ClientId = table.Column<string>(type: "TEXT", nullable: false),
                ClientManifestHash = table.Column<string>(type: "TEXT", nullable: true),
                FilesToUpdate = table.Column<int>(type: "INTEGER", nullable: false),
                RequestTimestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                RequestType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                ResponseTimeMs = table.Column<int>(type: "INTEGER", nullable: true),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                TotalSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                Username = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UpdateRequests", x => x.Id);
                table.ForeignKey(
                    name: "FK_UpdateRequests_Clients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UserClientAccesses",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                ClientId = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UserId = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserClientAccesses", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserClientAccesses_Clients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_UserClientAccesses_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UserGroupAccesses",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                GroupId = table.Column<string>(type: "TEXT", nullable: false),
                UserId = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserGroupAccesses", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserGroupAccesses_Groups_GroupId",
                    column: x => x.GroupId,
                    principalTable: "Groups",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_UserGroupAccesses_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UpdateDetails",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ManifestEntryId = table.Column<string>(type: "TEXT", nullable: true),
                NewMd5Hash = table.Column<string>(type: "TEXT", nullable: false),
                OldMd5Hash = table.Column<string>(type: "TEXT", nullable: true),
                RelativePath = table.Column<string>(type: "TEXT", nullable: false),
                SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                UpdateRequestId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UpdateDetails", x => x.Id);
                table.ForeignKey(
                    name: "FK_UpdateDetails_ManifestEntries_ManifestEntryId",
                    column: x => x.ManifestEntryId,
                    principalTable: "ManifestEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_UpdateDetails_UpdateRequests_UpdateRequestId",
                    column: x => x.UpdateRequestId,
                    principalTable: "UpdateRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ClientBlockHistories_ClientId_CreatedAt",
            table: "ClientBlockHistories",
            columns: ["ClientId", "CreatedAt"]);

        migrationBuilder.CreateIndex(
            name: "IX_ClientComputerInfos_ClientId",
            table: "ClientComputerInfos",
            column: "ClientId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ClientComputerInfos_HardwareFingerprint",
            table: "ClientComputerInfos",
            column: "HardwareFingerprint");

        migrationBuilder.CreateIndex(
            name: "IX_ClientHistories_ClientId_ChangeTimestamp",
            table: "ClientHistories",
            columns: ["ClientId", "ChangeTimestamp"]);

        migrationBuilder.CreateIndex(
            name: "IX_ClientNetworkInfos_ClientId_IpAddress",
            table: "ClientNetworkInfos",
            columns: ["ClientId", "IpAddress"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Clients_GroupId",
            table: "Clients",
            column: "GroupId");

        migrationBuilder.CreateIndex(
            name: "IX_Clients_IsActive",
            table: "Clients",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_EnrollmentRequests_ClientId",
            table: "EnrollmentRequests",
            column: "ClientId");

        migrationBuilder.CreateIndex(
            name: "IX_EnrollmentRequests_Status",
            table: "EnrollmentRequests",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_FileChanges_ChangeTimestamp",
            table: "FileChanges",
            column: "ChangeTimestamp");

        migrationBuilder.CreateIndex(
            name: "IX_FileChanges_ManifestEntryId",
            table: "FileChanges",
            column: "ManifestEntryId");

        migrationBuilder.CreateIndex(
            name: "IX_Groups_Name",
            table: "Groups",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ManifestEntries_RelativePath",
            table: "ManifestEntries",
            column: "RelativePath",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_Token",
            table: "RefreshTokens",
            column: "Token",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_UserId",
            table: "RefreshTokens",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_UpdateDetails_ManifestEntryId",
            table: "UpdateDetails",
            column: "ManifestEntryId");

        migrationBuilder.CreateIndex(
            name: "IX_UpdateDetails_UpdateRequestId",
            table: "UpdateDetails",
            column: "UpdateRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_UpdateRequests_ClientId_RequestTimestamp",
            table: "UpdateRequests",
            columns: ["ClientId", "RequestTimestamp"]);

        migrationBuilder.CreateIndex(
            name: "IX_UpdateRequests_RequestTimestamp",
            table: "UpdateRequests",
            column: "RequestTimestamp");

        migrationBuilder.CreateIndex(
            name: "IX_UserClientAccesses_ClientId",
            table: "UserClientAccesses",
            column: "ClientId");

        migrationBuilder.CreateIndex(
            name: "IX_UserClientAccesses_UserId_ClientId",
            table: "UserClientAccesses",
            columns: ["UserId", "ClientId"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserGroupAccesses_GroupId",
            table: "UserGroupAccesses",
            column: "GroupId");

        migrationBuilder.CreateIndex(
            name: "IX_UserGroupAccesses_UserId_GroupId",
            table: "UserGroupAccesses",
            columns: ["UserId", "GroupId"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_Username",
            table: "Users",
            column: "Username",
            unique: true);
    }

    /// <summary>Удаляет всю схему.</summary>
    /// <param name="migrationBuilder">Построитель операций миграции.</param>
    /// <remarks>Таблицы удаляются в обратном порядке — от ссылающихся к тем, на которые ссылаются.</remarks>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ClientBlockHistories");
        migrationBuilder.DropTable(name: "ClientComputerInfos");
        migrationBuilder.DropTable(name: "ClientHistories");
        migrationBuilder.DropTable(name: "ClientNetworkInfos");
        migrationBuilder.DropTable(name: "EnrollmentRequests");
        migrationBuilder.DropTable(name: "FileChanges");
        migrationBuilder.DropTable(name: "RefreshTokens");
        migrationBuilder.DropTable(name: "UpdateDetails");
        migrationBuilder.DropTable(name: "UserClientAccesses");
        migrationBuilder.DropTable(name: "UserGroupAccesses");
        migrationBuilder.DropTable(name: "ManifestEntries");
        migrationBuilder.DropTable(name: "UpdateRequests");
        migrationBuilder.DropTable(name: "Users");
        migrationBuilder.DropTable(name: "Clients");
        migrationBuilder.DropTable(name: "Groups");
    }
}
