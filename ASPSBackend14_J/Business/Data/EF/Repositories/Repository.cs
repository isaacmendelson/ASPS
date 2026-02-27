using Common.Models;
using Interface.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Business.Data.EF.Repositories;

public class Repository<T> : IRepository<T> where T : Entity
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByKeyAsync(Key key)
    {
        var keyField = Entity.GetDbKey(key);
        return await _dbSet.FirstOrDefaultAsync(e => e.KeyField == keyField && !e.IsDeleted);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        var allItems = await _context.Set<T>()
            .AsNoTracking()
            .ToListAsync();

        return allItems.Where(e => !e.IsDeleted).ToList();
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        // Generate new GUID if not set
        // Note: KeyField setter is protected, so entities should set it before calling AddAsync
        // or we rely on database default/trigger
        
        entity.DateCreated = DateTime.UtcNow;
        await _dbSet.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public virtual async Task UpdateAsync(T entity)
    {
        entity.DateModified = DateTime.UtcNow;
        _dbSet.Update(entity);
        await _context.SaveChangesAsync();
    }

    public virtual async Task DeleteAsync(Key key)
    {
        var entity = await GetByKeyAsync(key);
        if (entity != null)
        {
            entity.IsDeleted = true;
            entity.DateDeleted = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public virtual async Task<bool> ExistsAsync(Key key)
    {
        var keyField = Entity.GetDbKey(key);
        return await _dbSet.AnyAsync(e => e.KeyField == keyField && !e.IsDeleted);
    }
}
