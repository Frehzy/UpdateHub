using UpdateHub.BackendServer.Application.Abstractions.Repositories.Clients;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Enrollments;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Groups;
using UpdateHub.BackendServer.Application.Abstractions.Repositories;
using UpdateHub.BackendServer.Application.Abstractions.Services.Clients;
using UpdateHub.BackendServer.Application.Abstractions.Services.Enrollments;
using UpdateHub.BackendServer.Application.Sync;
using UpdateHub.BackendServer.Domain.Entities.Clients;
using UpdateHub.BackendServer.Domain.Entities.Enrollments;
using UpdateHub.BackendServer.Domain.Enums;
using UpdateHub.Shared.Enums;

namespace UpdateHub.BackendServer.Application.Services.Enrollments;

/// <summary>Приём и рассмотрение заявок на регистрацию компьютеров.</summary>
/// <param name="enrollmentRepository">Доступ к заявкам.</param>
/// <param name="clientRepository">Доступ к компьютерам.</param>
/// <param name="computerInfoRepository">Доступ к сведениям о железе.</param>
/// <param name="groupRepository">Доступ к группам.</param>
/// <param name="clientService">Управление компьютерами.</param>
/// <param name="logger">Журнал.</param>
public class EnrollmentService(
    IEnrollmentRequestRepository enrollmentRepository,
    IClientRepository clientRepository,
    IClientComputerInfoRepository computerInfoRepository,
    IGroupRepository groupRepository,
    IClientService clientService,
    ILogger<EnrollmentService> logger) : IEnrollmentService
{
    /// <inheritdoc />
    public async Task<EnrollmentRequestEntity> SubmitAsync(
        EnrollmentSubmission request,
        string? remoteIpAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            throw new ArgumentException("Идентификатор компьютера не может быть пустым");
        }

        // Повторная подача с того же компьютера не плодит заявки: обновляем
        // сведения в уже поданной, чтобы администратор видел актуальное состояние.
        var pending = await enrollmentRepository.GetPendingByClientIdAsync(request.ClientId, cancellationToken);
        if (pending is not null)
        {
            pending.HardwareFingerprint = request.HardwareFingerprint ?? pending.HardwareFingerprint;
            pending.Hostname = request.Hostname ?? pending.Hostname;
            pending.OsVersion = request.OsVersion ?? pending.OsVersion;
            pending.RequestedByUsername = request.Username ?? pending.RequestedByUsername;
            pending.Comment = request.Comment ?? pending.Comment;
            pending.RemoteIpAddress = remoteIpAddress;
            await enrollmentRepository.UpdateAsync(pending, cancellationToken);

            logger.LogInformation("Обновлена заявка на регистрацию компьютера {ClientId}", request.ClientId);
            return pending;
        }

        var entity = new EnrollmentRequestEntity
        {
            ClientId = request.ClientId,
            HardwareFingerprint = request.HardwareFingerprint,
            Hostname = request.Hostname,
            OsVersion = request.OsVersion,
            RequestedByUsername = request.Username,
            Comment = request.Comment,
            RemoteIpAddress = remoteIpAddress,
            Status = EnrollmentStatus.Pending
        };

        await enrollmentRepository.CreateAsync(entity, cancellationToken);

        logger.LogInformation(
            "Подана заявка на регистрацию компьютера {ClientId} ({Hostname}) пользователем {Username}",
            entity.ClientId, entity.Hostname, entity.RequestedByUsername);

        return entity;
    }

    /// <inheritdoc />
    public async Task<ClientEntity> ApproveAsync(
        string requestId,
        string? groupId,
        string resolvedBy,
        CancellationToken cancellationToken = default)
    {
        var request = await enrollmentRepository.GetByIdAsync(requestId, cancellationToken)
            ?? throw new EntityNotFoundException($"Заявка '{requestId}' не найдена");

        if (request.Status != EnrollmentStatus.Pending)
        {
            throw new InvalidOperationException("Заявка уже рассмотрена");
        }

        if (!string.IsNullOrEmpty(groupId) &&
            await groupRepository.GetByIdAsync(groupId, cancellationToken) is null)
        {
            throw new EntityNotFoundException($"Группа '{groupId}' не найдена");
        }

        var client = await clientRepository.GetByIdAsync(request.ClientId, cancellationToken);

        if (client is null)
        {
            client = new ClientEntity
            {
                Id = request.ClientId,
                GroupId = groupId,
                IsActive = true
            };

            await clientRepository.CreateAsync(client, cancellationToken);

            await computerInfoRepository.CreateAsync(new ClientComputerInfoEntity
            {
                ClientId = client.Id,
                Hostname = request.Hostname ?? "не указано",
                HardwareFingerprint = request.HardwareFingerprint,
                OsVersion = request.OsVersion
            }, cancellationToken);
        }
        else
        {
            // Компьютер мог быть заведён вручную или помечен удалённым — возвращаем его в строй.
            client.IsActive = true;
            client.GroupId = groupId ?? client.GroupId;
            client.UpdatedAt = DateTime.UtcNow;
            await clientRepository.UpdateAsync(client, cancellationToken);
        }

        await clientService.AddHistoryAsync(
            client.Id,
            ClientChangeType.Registered,
            null,
            $"Заявка одобрена ({resolvedBy})",
            cancellationToken);

        request.Status = EnrollmentStatus.Approved;
        request.ResolvedAt = DateTime.UtcNow;
        request.ResolvedBy = resolvedBy;
        await enrollmentRepository.UpdateAsync(request, cancellationToken);

        logger.LogInformation("Заявка {RequestId} одобрена пользователем {ResolvedBy}", requestId, resolvedBy);
        return client;
    }

    /// <inheritdoc />
    public async Task RejectAsync(string requestId, string resolvedBy, CancellationToken cancellationToken = default)
    {
        var request = await enrollmentRepository.GetByIdAsync(requestId, cancellationToken)
            ?? throw new EntityNotFoundException($"Заявка '{requestId}' не найдена");

        if (request.Status != EnrollmentStatus.Pending)
        {
            throw new InvalidOperationException("Заявка уже рассмотрена");
        }

        request.Status = EnrollmentStatus.Rejected;
        request.ResolvedAt = DateTime.UtcNow;
        request.ResolvedBy = resolvedBy;
        await enrollmentRepository.UpdateAsync(request, cancellationToken);

        logger.LogInformation("Заявка {RequestId} отклонена пользователем {ResolvedBy}", requestId, resolvedBy);
    }
}
