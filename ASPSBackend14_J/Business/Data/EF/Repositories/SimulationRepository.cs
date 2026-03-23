using Common.Entities;
using Common.Models;
using Interface.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Business.Data.EF.Repositories;

public class SimulationRepository : Repository<Simulation>, ISimulationRepository
{
    public SimulationRepository(AppDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Get all simulations created by a specific user
    /// </summary>
    public async Task<IEnumerable<Simulation>> GetByCreatorKeyAsync(Key creatorKey)
    {
        var creatorKeyField = Entity.GetDbKey(creatorKey);
        return await _dbSet
            .Where(s => s.CreatorKeyField == creatorKeyField && !s.IsDeleted)
            .Include(s => s.Creator)
            .OrderByDescending(s => s.DateCreated)
            .ToListAsync();
    }

    /// <summary>
    /// Search simulations by name, description, or creator name
    /// </summary>
    public async Task<IEnumerable<Simulation>> SearchAsync(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return await GetAllAsync();
        }

        var lowerSearch = searchText.ToLower();
        
        return await _dbSet
            .Include(s => s.Creator)
            .Where(s => !s.IsDeleted && 
                (s.Name.ToLower().Contains(lowerSearch) ||
                 s.Description.ToLower().Contains(lowerSearch) ||
                 s.KeyField.ToLower().Contains(lowerSearch) ||
                 (s.Creator != null && 
                  (s.Creator.FirstName.ToLower().Contains(lowerSearch) ||
                   s.Creator.LastName.ToLower().Contains(lowerSearch)))))
            .OrderByDescending(s => s.DateCreated)
            .ToListAsync();
    }

    /// <summary>
    /// Override GetAllAsync to include Creator navigation property
    /// </summary>
    public override async Task<IEnumerable<Simulation>> GetAllAsync()
    {
        return await _dbSet
            .Include(s => s.Creator)
            .Where(s => !s.IsDeleted)
            .OrderByDescending(s => s.DateCreated)
            .ToListAsync();
    }

    /// <summary>
    /// Override GetByKeyAsync to include Creator navigation property
    /// </summary>
    public override async Task<Simulation?> GetByKeyAsync(Key key)
    {
        var keyField = Entity.GetDbKey(key);
        return await _dbSet
            .Include(s => s.Creator)
            .FirstOrDefaultAsync(s => s.KeyField == keyField && !s.IsDeleted);
    }
}
