using UpdateHub.BackendServer.Domain.Entities;

namespace UpdateHub.BackendServer.Application.Abstractions.Services;

/// <summary>Чтение эталонного манифеста.</summary>
public interface IManifestService
{
    /// <summary>Возвращает запись манифеста по пути файла.</summary>
    /// <param name="relativePath">Путь относительно каталога раздачи.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Запись либо <see langword="null"/>.</returns>
    Task<ManifestEntryEntity?> GetEntryAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>Возвращает весь манифест.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список записей.</returns>
    Task<IReadOnlyList<ManifestEntryEntity>> GetAllEntriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Формирует текст манифеста в формате <c>md5sum</c>.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Текст манифеста.</returns>
    Task<string> RenderManifestAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает полный путь файла на диске, если он есть в манифесте.
    /// </summary>
    /// <param name="relativePath">Путь относительно каталога раздачи.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Запись манифеста и полный путь либо <see langword="null"/>.</returns>
    /// <remarks>
    /// Путь ищется точным совпадением в базе, а не склеивается из строки запроса.
    /// В манифест попадает только то, что сервер сам нашёл при обходе, поэтому
    /// выход за пределы каталога невозможен: такого пути там просто не окажется.
    /// </remarks>
    Task<(ManifestEntryEntity Entry, string FullPath)?> ResolveFileAsync(string relativePath, CancellationToken cancellationToken = default);
}
