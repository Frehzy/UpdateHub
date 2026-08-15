using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

public interface IManifestEntryRepository : IRepository<ManifestEntryEntity>
{
    Task<ManifestEntryEntity?> GetByPathAsync(string relativePath);
    new Task<IEnumerable<ManifestEntryEntity>> GetAllAsync();
}