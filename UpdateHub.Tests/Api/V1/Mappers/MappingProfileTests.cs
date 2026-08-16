using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using UpdateHub.Server.Api.V1.DTOs.Response;
using UpdateHub.Server.Api.V1.Mappers;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Domain.Enums;

namespace UpdateHub.Tests.Api.V1.Mappers;

/// <summary>
/// Проверяет правила преобразования сущностей в ответы панели управления.
/// </summary>
/// <remarks>
/// Ошибки в этих правилах не видны компилятору: свойства сопоставляются
/// по именам во время работы. Добавили поле в ответ, забыли описать источник —
/// и панель показывает пустоту вместо имени компьютера. Первый тест ловит
/// это разом: AutoMapper умеет сам сверить, что у каждого свойства ответа
/// есть источник.
/// </remarks>
public class MappingProfileTests
{
    /// <summary>Собирает настройку преобразований.</summary>
    /// <returns>Готовая настройка.</returns>
    private static MapperConfiguration CreateConfiguration()
        => new(options => options.AddProfile<MappingProfile>(), NullLoggerFactory.Instance);

    /// <summary>Создаёт преобразователь.</summary>
    /// <returns>Готовый преобразователь.</returns>
    private static IMapper CreateMapper() => CreateConfiguration().CreateMapper();

    /// <summary>
    /// У каждого свойства ответа есть источник: непокрытых полей нет.
    /// </summary>
    [Fact]
    public void Configuration_IsValid()
    {
        CreateConfiguration().AssertConfigurationIsValid();
    }

    /// <summary>
    /// Имя компьютера берётся из сведений о железе, а при их отсутствии
    /// подставляется понятная замена.
    /// </summary>
    /// <remarks>
    /// Компьютер заводится администратором до первого выхода на связь,
    /// и до него сведений о железе нет вовсе. Пустая строка в списке
    /// выглядела бы как сбой.
    /// </remarks>
    [Fact]
    public void ClientWithoutComputerInfo_NameReplacedWithPlaceholder()
    {
        var client = new ClientEntity { Id = "pc-1" };

        var dto = CreateMapper().Map<ClientResponseDto>(client);

        Assert.Equal("не указано", dto.Name);
        Assert.Null(dto.OsVersion);
        Assert.Null(dto.GroupName);
    }

    /// <summary>Имя компьютера и версия системы берутся из сведений о железе.</summary>
    [Fact]
    public void ClientWithComputerInfo_TakesNameAndOsVersion()
    {
        var client = new ClientEntity
        {
            Id = "pc-1",
            ComputerInfo = new ClientComputerInfoEntity
            {
                ClientId = "pc-1",
                Hostname = "buhgalteriya-01",
                OsVersion = "Astra Linux 1.7.6"
            }
        };

        var dto = CreateMapper().Map<ClientResponseDto>(client);

        Assert.Equal("buhgalteriya-01", dto.Name);
        Assert.Equal("Astra Linux 1.7.6", dto.OsVersion);
    }

    /// <summary>
    /// Из нескольких сетевых адресов показывается последний виденный
    /// среди действующих.
    /// </summary>
    /// <remarks>
    /// У машины может быть несколько интерфейсов и старые записи от прежних
    /// адресов. Администратору нужен тот, по которому её видели последним.
    /// </remarks>
    [Fact]
    public void ClientWithSeveralAddresses_ShowsMostRecentActive()
    {
        var client = new ClientEntity
        {
            Id = "pc-1",
            NetworkInfos =
            [
                new ClientNetworkInfoEntity
                {
                    ClientId = "pc-1",
                    IpAddress = "10.0.0.5",
                    IsActive = true,
                    LastSeen = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new ClientNetworkInfoEntity
                {
                    ClientId = "pc-1",
                    IpAddress = "10.0.0.9",
                    IsActive = true,
                    LastSeen = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new ClientNetworkInfoEntity
                {
                    ClientId = "pc-1",
                    IpAddress = "192.168.1.1",
                    IsActive = false,
                    LastSeen = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            ]
        };

        var dto = CreateMapper().Map<ClientResponseDto>(client);

        Assert.Equal("10.0.0.9", dto.IpAddress);
    }

    /// <summary>Причина блокировки показывается только у заблокированного компьютера.</summary>
    /// <remarks>
    /// История блокировок остаётся и после разблокировки. Показывать по ней
    /// причину у работающей машины нельзя — администратор решит, что она
    /// до сих пор заблокирована.
    /// </remarks>
    [Fact]
    public void UnblockedClient_DoesNotShowPreviousBlockReason()
    {
        var client = new ClientEntity
        {
            Id = "pc-1",
            IsBlocked = false,
            BlockHistory =
            [
                new ClientBlockHistoryEntity
                {
                    ClientId = "pc-1",
                    Action = "blocked",
                    Reason = "Старая причина",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            ]
        };

        var dto = CreateMapper().Map<ClientDetailResponseDto>(client);

        Assert.Null(dto.BlockedReason);
        Assert.Null(dto.BlockedAt);
    }

    /// <summary>У заблокированного компьютера показывается последняя причина.</summary>
    [Fact]
    public void BlockedClient_ShowsLatestBlockReason()
    {
        var client = new ClientEntity
        {
            Id = "pc-1",
            IsBlocked = true,
            BlockHistory =
            [
                new ClientBlockHistoryEntity
                {
                    ClientId = "pc-1",
                    Action = "blocked",
                    Reason = "Первая причина",
                    BlockedBy = "admin",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new ClientBlockHistoryEntity
                {
                    ClientId = "pc-1",
                    Action = "blocked",
                    Reason = "Свежая причина",
                    BlockedBy = "petrov",
                    CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            ]
        };

        var dto = CreateMapper().Map<ClientDetailResponseDto>(client);

        Assert.Equal("Свежая причина", dto.BlockedReason);
        Assert.Equal("petrov", dto.BlockedBy);
    }

    /// <summary>
    /// Роль отдаётся строкой: панель показывает её как есть, а числовое
    /// значение перечисления зависело бы от порядка объявления.
    /// </summary>
    [Fact]
    public void User_RoleMappedAsText()
    {
        var user = new UserEntity { Username = "ivanov", Role = UserRole.Admin };

        var dto = CreateMapper().Map<UserResponseDto>(user);

        Assert.Equal(nameof(UserRole.Admin), dto.Role);
    }

    /// <summary>
    /// В счёт компьютеров группы попадают только действующие.
    /// </summary>
    /// <remarks>
    /// Удалённые компьютеры остаются в базе ради истории обращений,
    /// но в списке групп их считать не нужно.
    /// </remarks>
    [Fact]
    public void Group_CountsOnlyActiveClients()
    {
        var group = new GroupEntity
        {
            Name = "Бухгалтерия",
            Clients =
            [
                new ClientEntity { Id = "pc-1", IsActive = true },
                new ClientEntity { Id = "pc-2", IsActive = true },
                new ClientEntity { Id = "pc-3", IsActive = false }
            ]
        };

        var dto = CreateMapper().Map<GroupResponseDto>(group);

        Assert.Equal(2, dto.ClientCount);
    }

    /// <summary>
    /// В выданных правах вместо идентификатора компьютера показывается его имя,
    /// а если имени нет — сам идентификатор.
    /// </summary>
    [Fact]
    public void ClientAccess_FallsBackToIdentifierWhenNameUnknown()
    {
        var withName = new UserClientAccessEntity
        {
            ClientId = "pc-1",
            Client = new ClientEntity
            {
                Id = "pc-1",
                ComputerInfo = new ClientComputerInfoEntity { ClientId = "pc-1", Hostname = "sklad-03" }
            }
        };

        var withoutName = new UserClientAccessEntity { ClientId = "pc-2" };

        var mapper = CreateMapper();

        Assert.Equal("sklad-03", mapper.Map<UserClientAccessDto>(withName).ClientName);
        Assert.Equal("pc-2", mapper.Map<UserClientAccessDto>(withoutName).ClientName);
    }

    /// <summary>Состояние заявки отдаётся строкой.</summary>
    [Fact]
    public void Enrollment_StatusMappedAsText()
    {
        var request = new EnrollmentRequestEntity { ClientId = "pc-1", Status = EnrollmentStatus.Pending };

        var dto = CreateMapper().Map<EnrollmentResponseDto>(request);

        Assert.Equal(nameof(EnrollmentStatus.Pending), dto.Status);
    }
}
