using Common.Models;

namespace Interface.Repositories;

public interface IRepository<T> where T : class
{
    Task<T?> GetByKeyAsync(Key key);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(Key key);
    Task<bool> ExistsAsync(Key key);
}
