namespace UpdateHub.Server.Application.Abstractions.Repositories;

public interface IRepository<T> where T : class
{
    Task<T> CreateAsync(T entity);
    Task<T?> GetByIdAsync(string id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> UpdateAsync(T entity);
    Task DeleteAsync(string id);
}