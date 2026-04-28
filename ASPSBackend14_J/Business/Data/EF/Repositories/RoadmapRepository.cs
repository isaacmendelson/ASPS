using Common.Entities;
using Interface.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Business.Data.EF.Repositories;

/// <summary>
/// EF Core implementation for Roadmap entity (JSON-blob design).
/// </summary>
public class RoadmapRepository : IRoadmapRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<RoadmapRepository> _logger;

    public RoadmapRepository(AppDbContext context, ILogger<RoadmapRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Roadmap?> GetByIdAsync(int id)
    {
        return await _context.Roadmaps.FirstOrDefaultAsync(r => r.Id == id && !r.IsArchived);
    }

    public async Task<IEnumerable<Roadmap>> ListAsync(bool includeArchived = false)
    {
        var q = _context.Roadmaps.AsQueryable();
        if (!includeArchived) q = q.Where(r => !r.IsArchived);
        return await q.OrderByDescending(r => r.LastUpdatedAt ?? r.DateCreated).ToListAsync();
    }

    public async Task<int> AddAsync(Roadmap roadmap)
    {
        roadmap.DateCreated = DateTime.UtcNow;
        if (roadmap.Version <= 0) roadmap.Version = 1;
        _context.Roadmaps.Add(roadmap);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Created roadmap '{Name}' (ID={Id}) by {User}",
            roadmap.Name, roadmap.Id, roadmap.CreatedBy ?? "unknown");
        return roadmap.Id;
    }

    public async Task<int?> UpdateAsync(int id, string data, int expectedVersion, string? updatedBy)
    {
        var roadmap = await _context.Roadmaps.FirstOrDefaultAsync(r => r.Id == id);
        if (roadmap is null) return null;

        // Optimistic concurrency: bail out if the caller's version is stale.
        if (roadmap.Version != expectedVersion)
        {
            _logger.LogWarning(
                "Roadmap save conflict: id={Id} expected={Expected} actual={Actual} user={User}",
                id, expectedVersion, roadmap.Version, updatedBy ?? "unknown");
            return null;
        }

        roadmap.Data = data;
        roadmap.Version = expectedVersion + 1;
        roadmap.LastUpdatedAt = DateTime.UtcNow;
        roadmap.LastUpdatedBy = updatedBy;
        await _context.SaveChangesAsync();
        return roadmap.Version;
    }

    public async Task<bool> UpdateMetadataAsync(int id, string name, string? description, string? updatedBy)
    {
        var roadmap = await _context.Roadmaps.FirstOrDefaultAsync(r => r.Id == id);
        if (roadmap is null) return false;
        roadmap.Name = name;
        roadmap.Description = description;
        roadmap.LastUpdatedAt = DateTime.UtcNow;
        roadmap.LastUpdatedBy = updatedBy;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ArchiveAsync(int id, string? updatedBy)
    {
        var roadmap = await _context.Roadmaps.FirstOrDefaultAsync(r => r.Id == id);
        if (roadmap is null) return false;
        roadmap.IsArchived = true;
        roadmap.LastUpdatedAt = DateTime.UtcNow;
        roadmap.LastUpdatedBy = updatedBy;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Archived roadmap ID={Id} by {User}", id, updatedBy ?? "unknown");
        return true;
    }
}
