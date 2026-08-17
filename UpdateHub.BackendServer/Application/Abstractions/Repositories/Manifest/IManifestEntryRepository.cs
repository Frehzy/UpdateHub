using UpdateHub.BackendServer.Domain.Entities.Manifest;

namespace UpdateHub.BackendServer.Application.Abstractions.Repositories.Manifest;

/// <summary>Доступ к записям эталонного манифеста.</summary>
public interface IManifestEntryRepository : IRepository<ManifestEntryEntity, string>
{
    /// <summary>Ищет запись по пути файла.</summary>
    /// <param name="relativePath">Путь относительно каталога раздачи.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Запись либо <see langword="null"/>.</returns>
    Task<ManifestEntryEntity?> GetByPathAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает весь манифест в виде словаря «путь — запись».
    /// Используется и сканером каталога, и сравнением с манифестом клиента.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Словарь записей.</returns>
    Task<Dictionary<string, ManifestEntryEntity>> GetAllByPathAsync(CancellationToken cancellationToken = default);

    /// <summary>Удаляет записи с указанными путями одним запросом.</summary>
    /// <param name="relativePaths">Пути к удалению.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Число удалённых записей.</returns>
    Task<int> DeleteByPathsAsync(IReadOnlyCollection<string> relativePaths, CancellationToken cancellationToken = default);
}
