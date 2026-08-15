using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

public interface IFileChangeRepository : IRepository<FileChangeEntity>
{
    Task<IEnumerable<FileChangeEntity>> GetOlderThanAsync(DateTime cutoff);
    Task<IEnumerable<FileChangeEntity>> GetUnprocessedAsync();
}