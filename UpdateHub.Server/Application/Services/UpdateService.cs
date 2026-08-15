using System.Security.Cryptography;
using System.Text;
using UpdateHub.Server.Api.V1.DTOs.Request;
using UpdateHub.Server.Api.V1.DTOs.Response;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Application.Abstractions.Services;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Domain.Enums;

namespace UpdateHub.Server.Application.Services;

public class UpdateService(
    IManifestService manifestService,
    IClientService clientService,
    IStatisticsService statisticsService,
    IManifestEntryRepository manifestEntryRepository,
    IUpdateRequestRepository updateRequestRepository,
    IUpdateDetailRepository updateDetailRepository,
    ILogger<UpdateService> logger) : IUpdateService
{
    public async Task<CheckResponseDto> CheckUpdatesAsync(CheckRequestDto request)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Проверяем, не обновляется ли манифест
        if (await manifestService.IsManifestUpdatingAsync())
        {
            throw new InvalidOperationException("Server is updating manifest, please try again later");
        }

        // Получаем или создаём клиента
        var client = await clientService.GetOrCreateClientAsync(request.ClientInfo!);

        // Получаем эталонный манифест
        var serverManifest = await manifestEntryRepository.GetAllAsync();
        var serverManifestDict = serverManifest.ToDictionary(m => m.RelativePath, m => m);

        // Сравниваем с клиентским манифестом
        var clientManifestDict = request.Manifest ?? [];

        var filesToUpdate = new List<FileUpdateInfoDto>();
        var filesToDelete = new List<string>();

        // Проверяем файлы на сервере
        foreach (var entry in serverManifestDict.Values)
        {
            if (!clientManifestDict.TryGetValue(entry.RelativePath, out var clientMd5) ||
                clientMd5 != entry.Md5Hash)
            {
                filesToUpdate.Add(new FileUpdateInfoDto
                {
                    Id = entry.Id,
                    RelativePath = entry.RelativePath,
                    SizeBytes = entry.SizeBytes,
                    Md5Hash = entry.Md5Hash
                });
            }
        }

        // Проверяем файлы, которых нет на сервере
        foreach (var clientPath in clientManifestDict.Keys)
        {
            if (!serverManifestDict.ContainsKey(clientPath))
            {
                filesToDelete.Add(clientPath);
            }
        }

        stopwatch.Stop();

        // Логируем запрос
        var status = filesToUpdate.Count != 0 ? UpdateStatus.Update : UpdateStatus.Ok;
        var totalSize = filesToUpdate.Sum(f => f.SizeBytes);

        var updateRequest = new UpdateRequestEntity
        {
            ClientId = client.Id,
            RequestTimestamp = DateTime.UtcNow,
            RequestType = RequestType.Check,
            ClientManifestHash = ComputeManifestHash(clientManifestDict),
            Status = status,
            FilesToUpdate = filesToUpdate.Count,
            TotalSizeBytes = totalSize,
            ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
        };

        await updateRequestRepository.CreateAsync(updateRequest);

        // Логируем статистику
        await statisticsService.LogUpdateRequestAsync(
            client.Id,
            RequestType.Check.ToString(),
            updateRequest.ClientManifestHash,
            status.ToString(),
            filesToUpdate.Count,
            totalSize,
            updateRequest.ResponseTimeMs);

        return new CheckResponseDto
        {
            Status = status.ToString().ToLower(),
            Files = filesToUpdate,
            DeleteFiles = filesToDelete.Count != 0 ? filesToDelete : null
        };
    }

    public async Task<CheckResponseDto> UpdateAsync(CheckRequestDto request)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Проверяем, не обновляется ли манифест
        if (await manifestService.IsManifestUpdatingAsync())
        {
            throw new InvalidOperationException("Server is updating manifest, please try again later");
        }

        // Получаем или создаём клиента
        var client = await clientService.GetOrCreateClientAsync(request.ClientInfo!);

        // Получаем эталонный манифест
        var serverManifest = await manifestEntryRepository.GetAllAsync();
        var serverManifestDict = serverManifest.ToDictionary(m => m.RelativePath, m => m);

        // Сравниваем с клиентским манифестом
        var clientManifestDict = request.Manifest ?? [];

        var filesToUpdate = new List<FileUpdateInfoDto>();
        var filesToDelete = new List<string>();
        var updateDetails = new List<(string Path, string? OldMd5, string NewMd5, long Size)>();

        // Проверяем файлы на сервере
        foreach (var entry in serverManifestDict.Values)
        {
            if (!clientManifestDict.TryGetValue(entry.RelativePath, out var clientMd5) ||
                clientMd5 != entry.Md5Hash)
            {
                filesToUpdate.Add(new FileUpdateInfoDto
                {
                    Id = entry.Id,
                    RelativePath = entry.RelativePath,
                    SizeBytes = entry.SizeBytes,
                    Md5Hash = entry.Md5Hash
                });

                updateDetails.Add((
                    entry.RelativePath,
                    clientManifestDict.GetValueOrDefault(entry.RelativePath),
                    entry.Md5Hash,
                    entry.SizeBytes
                ));
            }
        }

        // Проверяем файлы, которых нет на сервере
        foreach (var clientPath in clientManifestDict.Keys)
        {
            if (!serverManifestDict.ContainsKey(clientPath))
            {
                filesToDelete.Add(clientPath);
            }
        }

        stopwatch.Stop();

        // Логируем запрос
        var status = filesToUpdate.Count != 0 ? UpdateStatus.Update : UpdateStatus.Ok;
        var totalSize = filesToUpdate.Sum(f => f.SizeBytes);

        var updateRequest = new UpdateRequestEntity
        {
            ClientId = client.Id,
            RequestTimestamp = DateTime.UtcNow,
            RequestType = RequestType.Update,
            ClientManifestHash = ComputeManifestHash(clientManifestDict),
            Status = status,
            FilesToUpdate = filesToUpdate.Count,
            TotalSizeBytes = totalSize,
            ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
        };

        updateRequest = await updateRequestRepository.CreateAsync(updateRequest);

        // Если есть обновления, записываем детали
        if (filesToUpdate.Count != 0)
        {
            foreach (var detail in updateDetails)
            {
                var entry = serverManifestDict[detail.Path];
                var updateDetail = new UpdateDetailEntity
                {
                    UpdateRequestId = updateRequest.Id,
                    ManifestEntryId = entry.Id,
                    RelativePath = detail.Path,
                    OldMd5Hash = detail.OldMd5,
                    NewMd5Hash = detail.NewMd5,
                    SizeBytes = detail.Size
                };

                await updateDetailRepository.CreateAsync(updateDetail);
            }
        }

        // Логируем статистику
        await statisticsService.LogUpdateRequestAsync(
            client.Id,
            RequestType.Update.ToString(),
            updateRequest.ClientManifestHash,
            status.ToString(),
            filesToUpdate.Count,
            totalSize,
            updateRequest.ResponseTimeMs);

        return new CheckResponseDto
        {
            Status = status.ToString().ToLower(),
            Files = filesToUpdate,
            DeleteFiles = filesToDelete.Count != 0 ? filesToDelete : null
        };
    }

    private static string ComputeManifestHash(Dictionary<string, string> manifest)
    {
        if (manifest == null || manifest.Count == 0)
        {
            return string.Empty;
        }

        var sorted = manifest.OrderBy(kv => kv.Key);
        var sb = new StringBuilder();
        foreach (var kv in sorted)
        {
            sb.Append($"{kv.Key}:{kv.Value};");
        }
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToBase64String(bytes);
    }
}