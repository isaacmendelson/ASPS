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
        try
        {
            Console.WriteLine($"=== Repository<{typeof(T).Name}>.GetAllAsync START ===");
            
            // Load ALL records first without any filtering
            // This avoids Key comparison issues in SQL
            var allItems = await _context.Set<T>()
                .AsNoTracking()
                .ToListAsync();
            
            Console.WriteLine($"Total records loaded from database: {allItems.Count}");
            
            // Show each item
            foreach (var item in allItems)
            {
                Console.WriteLine($"  Item: Key={item.Key}, IsDeleted={item.IsDeleted}, IsDisabled={item.IsDisabled}");
            }
            
            // Filter IsDeleted in memory (client-side)
            var filtered = allItems.Where(e => !e.IsDeleted).ToList();
            Console.WriteLine($"Records after IsDeleted filter: {filtered.Count}");
            
            Console.WriteLine($"=== Repository<{typeof(T).Name}>.GetAllAsync END ===");
            
            return filtered;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"====================================");
            Console.WriteLine($"ERROR in Repository<{typeof(T).Name}>.GetAllAsync");
            Console.WriteLine($"====================================");
            Console.WriteLine($"Message: {ex.Message}");
            Console.WriteLine($"Type: {ex.GetType().Name}");
            Console.WriteLine($"Stack Trace:");
            Console.WriteLine(ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                Console.WriteLine($"Inner Stack:");
                Console.WriteLine(ex.InnerException.StackTrace);
            }
            Console.WriteLine($"====================================");
            return new List<T>();
        }
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
