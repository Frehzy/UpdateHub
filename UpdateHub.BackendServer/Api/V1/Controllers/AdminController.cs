using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Clients;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Enrollments;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Users;
using UpdateHub.BackendServer.Application.Abstractions.Services.Clients;
using UpdateHub.BackendServer.Application.Abstractions.Services.Enrollments;
using UpdateHub.BackendServer.Application.Abstractions.Services.Groups;
using UpdateHub.BackendServer.Application.Abstractions.Services.Manifest;
using UpdateHub.BackendServer.Application.Abstractions.Services.Updates;
using UpdateHub.BackendServer.Application.Abstractions.Services.Users;
using UpdateHub.BackendServer.Application.BackgroundServices;
using UpdateHub.BackendServer.Application.Maintenance;
using UpdateHub.BackendServer.Application.Manifest;
using UpdateHub.BackendServer.Application.Sync;
using UpdateHub.BackendServer.Infrastructure.Configuration;
using UpdateHub.BackendServer.Infrastructure.Diagnostics;
using UpdateHub.Shared.Contracts.Clients;
using UpdateHub.Shared.Contracts.Common;
using UpdateHub.Shared.Contracts.Enrollments;
using UpdateHub.Shared.Contracts.Groups;
using UpdateHub.Shared.Contracts.Maintenance;
using UpdateHub.Shared.Contracts.Manifest;
using UpdateHub.Shared.Contracts.Users;
using UpdateHub.Shared.Enums;

namespace UpdateHub.BackendServer.Api.V1.Controllers;

/// <summary>
/// Панель управления: пользователи, компьютеры, группы, права и манифест.
/// </summary>
/// <param name="clientService">Управление компьютерами.</param>
/// <param name="groupService">Управление группами и правами.</param>
/// <param name="authService">Создание учётных записей.</param>
/// <param name="statisticsService">Сводная статистика.</param>
/// <param name="backupService">Внеочередная резервная копия базы.</param>
/// <param name="backupState">Состояние резервного копирования.</param>
/// <param name="enrollmentService">Рассмотрение заявок.</param>
/// <param name="manifestScanService">Пересборка манифеста.</param>
/// <param name="manifestState">Состояние манифеста.</param>
/// <param name="config">Настройки: пути к каталогам и параметры копирования.</param>
/// <param name="userRepository">Доступ к учётным записям.</param>
/// <param name="refreshTokenRepository">Доступ к refresh-токенам.</param>
/// <param name="enrollmentRepository">Доступ к заявкам.</param>
/// <param name="computerInfoRepository">Доступ к сведениям о железе.</param>
/// <param name="mapper">Преобразование сущностей в модели ответа.</param>
/// <remarks>
/// Весь контроллер закрыт ролью администратора. Прежняя версия не проверяла
/// роль нигде: любой действующий токен, включая выданный обычному пользователю,
/// открывал все эти операции целиком.
/// </remarks>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = nameof(UserRole.Admin))]
[Produces("application/json")]
public class AdminController(
    IClientService clientService,
    IGroupService groupService,
    IAuthService authService,
    IStatisticsService statisticsService,
    BackupBackgroundService backupService,
    BackupState backupState,
    IEnrollmentService enrollmentService,
    IManifestScanService manifestScanService,
    ManifestState manifestState,
    IOptions<UpdateHubConfig> config,
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IEnrollmentRequestRepository enrollmentRepository,
    IClientComputerInfoRepository computerInfoRepository,
    IMapper mapper) : ApiControllerBase
{
    // ---------- Пользователи ----------

    /// <summary>Возвращает список учётных записей.</summary>
    /// <param name="role">Ограничение по роли.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список пользователей.</returns>
    /// <response code="200">Список получен.</response>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] UserRole? role, CancellationToken cancellationToken)
    {
        var users = role.HasValue
            ? await userRepository.GetByRoleAsync(role.Value, cancellationToken)
            : await userRepository.GetAllAsync(cancellationToken);

        var response = mapper.Map<List<UserResponseDto>>(users);
        return Ok(new UserListResponseDto { Users = response, Total = response.Count });
    }

    /// <summary>Возвращает учётную запись вместе с выданными ей правами.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сведения о пользователе.</returns>
    /// <response code="200">Пользователь найден.</response>
    /// <response code="404">Пользователь не найден.</response>
    [HttpGet("users/{userId}")]
    public async Task<IActionResult> GetUser(string userId, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdWithAccessAsync(userId, cancellationToken)
            ?? throw new EntityNotFoundException($"Пользователь '{userId}' не найден");

        return Ok(mapper.Map<UserResponseDto>(user));
    }

    /// <summary>Создаёт учётную запись.</summary>
    /// <param name="request">Параметры создания.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Созданный пользователь.</returns>
    /// <response code="201">Пользователь создан.</response>
    /// <response code="400">Пароль не удовлетворяет требованиям.</response>
    /// <response code="409">Логин уже занят.</response>
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequestDto request,
        CancellationToken cancellationToken)
    {
        var user = await authService.CreateUserAsync(
            request.Username,
            request.Password,
            request.Role,
            request.GroupIds,
            request.ClientIds,
            cancellationToken);

        return CreatedAtAction(nameof(GetUser), new { userId = user.Id }, mapper.Map<UserResponseDto>(user));
    }

    /// <summary>Включает или отключает учётную запись.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="request">Новое состояние.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Обновлённое состояние.</returns>
    /// <response code="200">Состояние изменено.</response>
    /// <response code="404">Пользователь не найден.</response>
    [HttpPut("users/{userId}/status")]
    public async Task<IActionResult> ToggleUserStatus(
        string userId,
        [FromBody] ToggleUserStatusRequestDto request,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new EntityNotFoundException($"Пользователь '{userId}' не найден");

        user.IsActive = request.IsActive;
        await userRepository.UpdateAsync(user, cancellationToken);

        // Отключённая запись не должна продолжать работать по ранее выданным токенам.
        if (!request.IsActive)
        {
            await refreshTokenRepository.RevokeAllForUserAsync(userId, cancellationToken);
        }

        return Ok(new { user.Id, user.IsActive });
    }

    /// <summary>Отключает учётную запись без удаления её истории.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Пустой ответ.</returns>
    /// <response code="204">Запись отключена.</response>
    /// <response code="404">Пользователь не найден.</response>
    [HttpDelete("users/{userId}")]
    public async Task<IActionResult> DeleteUser(string userId, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new EntityNotFoundException($"Пользователь '{userId}' не найден");

        user.IsActive = false;
        await userRepository.UpdateAsync(user, cancellationToken);
        await refreshTokenRepository.RevokeAllForUserAsync(userId, cancellationToken);

        return NoContent();
    }

    // ---------- Права ----------

    /// <summary>Выдаёт пользователю права на компьютер.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Подтверждение.</returns>
    /// <response code="200">Права выданы.</response>
    /// <response code="404">Пользователь или компьютер не найдены.</response>
    [HttpPut("users/{userId}/clients/{clientId}")]
    public async Task<IActionResult> GrantClientAccess(string userId, string clientId, CancellationToken cancellationToken)
    {
        await groupService.GrantClientAccessAsync(userId, clientId, cancellationToken);
        return Ok(new { status = "ok" });
    }

    /// <summary>Отзывает права пользователя на компьютер.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Пустой ответ.</returns>
    /// <response code="204">Права отозваны.</response>
    /// <response code="404">Разрешение не найдено.</response>
    [HttpDelete("users/{userId}/clients/{clientId}")]
    public async Task<IActionResult> RevokeClientAccess(string userId, string clientId, CancellationToken cancellationToken)
    {
        await groupService.RevokeClientAccessAsync(userId, clientId, cancellationToken);
        return NoContent();
    }

    /// <summary>Выдаёт пользователю права на группу компьютеров.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="groupId">Идентификатор группы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Подтверждение.</returns>
    /// <response code="200">Права выданы.</response>
    /// <response code="404">Пользователь или группа не найдены.</response>
    [HttpPut("users/{userId}/groups/{groupId}")]
    public async Task<IActionResult> GrantGroupAccess(string userId, string groupId, CancellationToken cancellationToken)
    {
        await groupService.GrantGroupAccessAsync(userId, groupId, cancellationToken);
        return Ok(new { status = "ok" });
    }

    /// <summary>Отзывает права пользователя на группу.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="groupId">Идентификатор группы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Пустой ответ.</returns>
    /// <response code="204">Права отозваны.</response>
    /// <response code="404">Разрешение не найдено.</response>
    [HttpDelete("users/{userId}/groups/{groupId}")]
    public async Task<IActionResult> RevokeGroupAccess(string userId, string groupId, CancellationToken cancellationToken)
    {
        await groupService.RevokeGroupAccessAsync(userId, groupId, cancellationToken);
        return NoContent();
    }

    // ---------- Компьютеры ----------

    /// <summary>Возвращает список компьютеров.</summary>
    /// <param name="groupId">Ограничение по группе.</param>
    /// <param name="isBlocked">Ограничение по признаку блокировки.</param>
    /// <param name="search">Строка поиска по идентификатору и имени.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список компьютеров.</returns>
    /// <response code="200">Список получен.</response>
    [HttpGet("clients")]
    public async Task<IActionResult> GetClients(
        [FromQuery] string? groupId,
        [FromQuery] bool? isBlocked,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var clients = await clientService.GetAllAsync(groupId, isBlocked, search, cancellationToken);
        var response = mapper.Map<List<ClientResponseDto>>(clients);
        return Ok(new ClientListResponseDto { Clients = response, Total = response.Count });
    }

    /// <summary>Возвращает подробные сведения о компьютере.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сведения о компьютере.</returns>
    /// <response code="200">Компьютер найден.</response>
    /// <response code="404">Компьютер не найден.</response>
    [HttpGet("clients/{clientId}")]
    public async Task<IActionResult> GetClient(string clientId, CancellationToken cancellationToken)
        => Ok(await clientService.GetDetailAsync(clientId, cancellationToken));

    /// <summary>Регистрирует компьютер вручную.</summary>
    /// <param name="request">Параметры регистрации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Созданный компьютер.</returns>
    /// <response code="201">Компьютер зарегистрирован.</response>
    /// <response code="409">Компьютер с таким идентификатором уже есть.</response>
    [HttpPost("clients")]
    public async Task<IActionResult> CreateClient(
        [FromBody] CreateClientRequestDto request,
        CancellationToken cancellationToken)
    {
        var client = await clientService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetClient), new { clientId = client.Id }, new { client.Id, client.GroupId });
    }

    /// <summary>Изменяет имя и группу компьютера.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="request">Новые значения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Обновлённые сведения.</returns>
    /// <response code="200">Изменения сохранены.</response>
    /// <response code="404">Компьютер или группа не найдены.</response>
    [HttpPut("clients/{clientId}")]
    public async Task<IActionResult> UpdateClient(
        string clientId,
        [FromBody] UpdateClientRequestDto request,
        CancellationToken cancellationToken)
    {
        var client = await clientService.UpdateAsync(clientId, request, cancellationToken);
        return Ok(new { client.Id, client.GroupId, client.IsBlocked, client.IsActive });
    }

    /// <summary>Помечает компьютер удалённым.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Пустой ответ.</returns>
    /// <response code="204">Компьютер помечен удалённым.</response>
    /// <response code="404">Компьютер не найден.</response>
    [HttpDelete("clients/{clientId}")]
    public async Task<IActionResult> DeleteClient(string clientId, CancellationToken cancellationToken)
    {
        await clientService.DeleteAsync(clientId, cancellationToken);
        return NoContent();
    }

    /// <summary>Блокирует компьютер.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="request">Причина блокировки.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Подтверждение.</returns>
    /// <response code="200">Компьютер заблокирован.</response>
    /// <response code="404">Компьютер не найден.</response>
    [HttpPost("clients/{clientId}/block")]
    public async Task<IActionResult> BlockClient(
        string clientId,
        [FromBody] BlockClientRequestDto request,
        CancellationToken cancellationToken)
    {
        await clientService.BlockAsync(clientId, request.Reason, CurrentUsername, cancellationToken);
        return Ok(new { status = "ok" });
    }

    /// <summary>Снимает блокировку с компьютера.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Подтверждение.</returns>
    /// <response code="200">Блокировка снята.</response>
    /// <response code="404">Компьютер не найден.</response>
    [HttpPost("clients/{clientId}/unblock")]
    public async Task<IActionResult> UnblockClient(string clientId, CancellationToken cancellationToken)
    {
        await clientService.UnblockAsync(clientId, CurrentUsername, cancellationToken);
        return Ok(new { status = "ok" });
    }

    // ---------- Группы ----------

    /// <summary>Возвращает список активных групп.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список групп.</returns>
    /// <response code="200">Список получен.</response>
    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups(CancellationToken cancellationToken)
    {
        var groups = await groupService.GetAllAsync(cancellationToken);
        return Ok(new GroupListResponseDto { Groups = [.. groups], Total = groups.Count });
    }

    /// <summary>Возвращает группу вместе с её составом.</summary>
    /// <param name="groupId">Идентификатор группы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сведения о группе.</returns>
    /// <response code="200">Группа найдена.</response>
    /// <response code="404">Группа не найдена.</response>
    [HttpGet("groups/{groupId}")]
    public async Task<IActionResult> GetGroup(string groupId, CancellationToken cancellationToken)
        => Ok(await groupService.GetDetailAsync(groupId, cancellationToken));

    /// <summary>Создаёт группу компьютеров.</summary>
    /// <param name="request">Параметры создания.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Созданная группа.</returns>
    /// <response code="201">Группа создана.</response>
    /// <response code="409">Группа с таким названием уже есть.</response>
    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup(
        [FromBody] CreateGroupRequestDto request,
        CancellationToken cancellationToken)
    {
        var group = await groupService.CreateAsync(request.Name, request.Description, cancellationToken);
        return CreatedAtAction(nameof(GetGroup), new { groupId = group.Id }, new { group.Id, group.Name });
    }

    /// <summary>Изменяет группу.</summary>
    /// <param name="groupId">Идентификатор группы.</param>
    /// <param name="request">Новые значения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Обновлённая группа.</returns>
    /// <response code="200">Изменения сохранены.</response>
    /// <response code="404">Группа не найдена.</response>
    [HttpPut("groups/{groupId}")]
    public async Task<IActionResult> UpdateGroup(
        string groupId,
        [FromBody] UpdateGroupRequestDto request,
        CancellationToken cancellationToken)
    {
        var group = await groupService.UpdateAsync(groupId, request.Name, request.Description, cancellationToken);
        return Ok(new { group.Id, group.Name, group.Description });
    }

    /// <summary>Помечает группу удалённой.</summary>
    /// <param name="groupId">Идентификатор группы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Пустой ответ.</returns>
    /// <response code="204">Группа помечена удалённой.</response>
    /// <response code="404">Группа не найдена.</response>
    [HttpDelete("groups/{groupId}")]
    public async Task<IActionResult> DeleteGroup(string groupId, CancellationToken cancellationToken)
    {
        await groupService.DeleteAsync(groupId, cancellationToken);
        return NoContent();
    }

    // ---------- Заявки на регистрацию ----------

    /// <summary>Возвращает заявки на регистрацию компьютеров.</summary>
    /// <param name="status">Ограничение по состоянию; по умолчанию — ожидающие рассмотрения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список заявок.</returns>
    /// <response code="200">Список получен.</response>
    /// <remarks>
    /// К каждой заявке прикладываются компьютеры с таким же отпечатком железа:
    /// после переустановки системы идентификатор меняется, и эта подсказка
    /// позволяет понять, что машина уже известна.
    /// </remarks>
    [HttpGet("enrollments")]
    public async Task<IActionResult> GetEnrollments(
        [FromQuery] EnrollmentStatus? status,
        CancellationToken cancellationToken)
    {
        var requests = await enrollmentRepository.GetByStatusAsync(
            status ?? EnrollmentStatus.Pending,
            cancellationToken);

        var response = mapper.Map<List<EnrollmentResponseDto>>(requests);

        foreach (var item in response.Where(r => !string.IsNullOrEmpty(r.HardwareFingerprint)))
        {
            var matches = await computerInfoRepository.GetByFingerprintAsync(item.HardwareFingerprint!, cancellationToken);
            item.MatchingClientIds = [.. matches.Select(m => m.ClientId).Where(id => id != item.ClientId)];
        }

        return Ok(new EnrollmentListResponseDto { Enrollments = response, Total = response.Count });
    }

    /// <summary>Одобряет заявку и заводит компьютер.</summary>
    /// <param name="requestId">Идентификатор заявки.</param>
    /// <param name="request">Группа, в которую поместить компьютер.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Созданный компьютер.</returns>
    /// <response code="200">Заявка одобрена.</response>
    /// <response code="404">Заявка или группа не найдены.</response>
    /// <response code="409">Заявка уже рассмотрена.</response>
    [HttpPost("enrollments/{requestId}/approve")]
    public async Task<IActionResult> ApproveEnrollment(
        string requestId,
        [FromBody] ApproveEnrollmentRequestDto request,
        CancellationToken cancellationToken)
    {
        var client = await enrollmentService.ApproveAsync(requestId, request.GroupId, CurrentUsername, cancellationToken);
        return Ok(new { status = "ok", clientId = client.Id, client.GroupId });
    }

    /// <summary>Отклоняет заявку.</summary>
    /// <param name="requestId">Идентификатор заявки.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Пустой ответ.</returns>
    /// <response code="204">Заявка отклонена.</response>
    /// <response code="404">Заявка не найдена.</response>
    /// <response code="409">Заявка уже рассмотрена.</response>
    [HttpPost("enrollments/{requestId}/reject")]
    public async Task<IActionResult> RejectEnrollment(string requestId, CancellationToken cancellationToken)
    {
        await enrollmentService.RejectAsync(requestId, CurrentUsername, cancellationToken);
        return NoContent();
    }

    // ---------- Манифест и статистика ----------

    /// <summary>Возвращает состояние эталонного манифеста.</summary>
    /// <returns>Состояние манифеста.</returns>
    /// <response code="200">Состояние получено.</response>
    [HttpGet("manifest/status")]
    public IActionResult GetManifestStatus()
        => Ok(new ManifestStatusResponseDto
        {
            Generation = manifestState.Generation,
            IsScanning = manifestState.IsScanning,
            LastScanCompletedAt = manifestState.LastScanCompletedAt,
            EntryCount = manifestState.EntryCount,
            TotalSizeBytes = manifestState.TotalSizeBytes,
            RejectedPaths = manifestState.RejectedPaths
        });

    /// <summary>Запускает внеочередной обход каталога раздачи.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Итоги обхода.</returns>
    /// <response code="200">Обход выполнен.</response>
    /// <response code="409">Обход уже выполняется.</response>
    [HttpPost("manifest/rescan")]
    public async Task<IActionResult> RescanManifest(CancellationToken cancellationToken)
    {
        var result = await manifestScanService.ScanAsync(cancellationToken);

        if (!result.Executed)
        {
            return Conflict(new ErrorResponseDto { Error = "Обход каталога уже выполняется" });
        }

        return Ok(new RescanResultDto
        {
            Status = "ok",
            TotalFiles = result.TotalFiles,
            HashedFiles = result.HashedFiles,
            Changes = result.Changes,
            RejectedPaths = result.RejectedPaths
        });
    }

    /// <summary>Возвращает сводную статистику обращений.</summary>
    /// <param name="days">Глубина периода в сутках; без параметра — за всё время.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сводка.</returns>
    /// <response code="200">Статистика получена.</response>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] int? days, CancellationToken cancellationToken)
        => Ok(await statisticsService.GetStatisticsAsync(days, cancellationToken));

    /// <summary>
    /// Возвращает компьютеры, давно не выходившие на связь.
    /// </summary>
    /// <param name="days">Порог в сутках; если не указан, берётся из настроек.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список компьютеров, начиная с самых давних.</returns>
    /// <remarks>
    /// Отвечает на вопрос, который администратор задаёт каждое утро: какие
    /// машины перестали обновляться. Сводка обращений на него не отвечает —
    /// она показывает итог по всем, а молчащий компьютер в итоге незаметен.
    /// </remarks>
    [HttpGet("clients/stale")]
    public async Task<IActionResult> GetStaleClients([FromQuery] int? days, CancellationToken cancellationToken)
        => Ok(await statisticsService.GetStaleClientsAsync(days, cancellationToken));

    /// <summary>
    /// Снимает резервную копию базы данных по требованию.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Путь и размер снятой копии.</returns>
    /// <remarks>
    /// Копии снимаются и по расписанию. Кнопка нужна перед тем, как что-то
    /// менять руками: ждать до ночи в такой момент неразумно.
    /// </remarks>
    [HttpPost("backup")]
    public async Task<IActionResult> CreateBackup(CancellationToken cancellationToken)
    {
        var path = await backupService.CreateBackupAsync(cancellationToken);

        if (path is null)
        {
            return Ok(new BackupResultDto
            {
                Created = false,
                Message = "Копию снять не удалось. Подробности в журнале сервера"
            });
        }

        return Ok(new BackupResultDto
        {
            Created = true,
            Path = path,
            SizeBytes = new FileInfo(path).Length,
            Message = "Копия снята"
        });
    }

    /// <summary>
    /// Возвращает состояние обслуживания: резервные копии и место на дисках.
    /// </summary>
    /// <returns>Сводка обслуживания.</returns>
    /// <remarks>
    /// Заведено потому, что узнать о работе копирования было нельзя иначе как
    /// заглянув в папку на сервере. Служба при неудаче не роняет сервер, и это
    /// верно — раздача файлов важнее копий, — но означает, что отказавшее
    /// копирование остаётся незамеченным до того дня, когда копия понадобится.
    /// <para>
    /// Место на дисках отдаётся здесь же и только администратору: клиенту знать
    /// о хозяйстве сервера незачем, он приходит за файлами.
    /// </para>
    /// </remarks>
    [HttpGet("maintenance")]
    public IActionResult GetMaintenanceStatus()
    {
        var backupDirectory = config.Value.ResolvedBackupPath;

        var (backupFree, backupTotal) = DiskSpace.Measure(backupDirectory);
        var (filesFree, filesTotal) = DiskSpace.Measure(config.Value.ResolvedFilesPath);

        var status = new MaintenanceStatusDto
        {
            LastAttemptAt = backupState.Last?.At,
            LastAttemptSucceeded = backupState.Last?.Succeeded ?? false,
            LastAttemptError = backupState.Last?.Error,

            LastSuccessAt = backupState.LastSuccess?.At,
            LastSuccessSizeBytes = backupState.LastSuccess?.SizeBytes ?? 0,
            LastSuccessPath = backupState.LastSuccess?.Path,

            SuccessCount = backupState.SuccessCount,
            FailureCount = backupState.FailureCount,

            // Число файлов берётся с диска, а не из счётчика попыток: только оно
            // переживает перезапуск сервера и показывает настоящий запас копий.
            BackupFilesOnDisk = Directory.Exists(backupDirectory)
                ? Directory.GetFiles(backupDirectory, "updatehub-*.db").Length
                : 0,

            BackupPath = backupDirectory,
            IntervalHours = config.Value.BackupIntervalHours,
            KeepCount = config.Value.BackupKeepCount,

            BackupFreeBytes = backupFree,
            BackupTotalBytes = backupTotal,
            FilesFreeBytes = filesFree,
            FilesTotalBytes = filesTotal
        };

        return Ok(status);
    }
}
