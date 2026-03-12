using Common.Entities;
using Interface.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Business.Data.EF.Repositories;

public class TrackedDomainRepository : ITrackedDomainRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<TrackedDomainRepository> _logger;

    public TrackedDomainRepository(
        AppDbContext context,
        ILogger<TrackedDomainRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TrackedDomain?> GetByIdAsync(int id)
    {
        return await _context.TrackedDomains
            .FirstOrDefaultAsync(d => d.Id == id && d.DateDeleted == null);
    }

    public async Task<IEnumerable<TrackedDomain>> GetAllActiveAsync()
    {
        return await _context.TrackedDomains
            .Where(d => d.DateDeleted == null && d.IsActive)
            .OrderBy(d => d.Category)
            .ThenBy(d => d.Domain)
            .ToListAsync();
    }

    public async Task<TrackedDomain?> GetByDomainAsync(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return null;

        var normalizedDomain = domain.ToLowerInvariant().Trim();

        return await _context.TrackedDomains
            .FirstOrDefaultAsync(d => d.Domain == normalizedDomain && d.DateDeleted == null);
    }

    public async Task<IEnumerable<TrackedDomain>> GetByCategoryAsync(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return Enumerable.Empty<TrackedDomain>();

        return await _context.TrackedDomains
            .Where(d => d.Category == category && d.DateDeleted == null && d.IsActive)
            .OrderBy(d => d.Domain)
            .ToListAsync();
    }

    public async Task<bool> IsTrackedDomainAsync(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return false;

        var normalizedDomain = domain.ToLowerInvariant().Trim();

        return await _context.TrackedDomains
            .AnyAsync(d => d.Domain == normalizedDomain && d.DateDeleted == null && d.IsActive);
    }

    public async Task<int> AddAsync(TrackedDomain trackedDomain)
    {
        _context.TrackedDomains.Add(trackedDomain);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Added tracked domain: {trackedDomain.Domain} (Category: {trackedDomain.Category}, ID: {trackedDomain.Id})");

        return trackedDomain.Id;
    }

    public async Task<int> AddRangeAsync(IEnumerable<TrackedDomain> trackedDomains)
    {
        var domainsList = trackedDomains.ToList();

        _context.TrackedDomains.AddRange(domainsList);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Added {domainsList.Count} tracked domains");

        return domainsList.Count;
    }

    public async Task UpdateAsync(TrackedDomain trackedDomain)
    {
        trackedDomain.DateModified = DateTime.UtcNow;
        _context.TrackedDomains.Update(trackedDomain);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Updated tracked domain ID: {trackedDomain.Id}");
    }

    public async Task DeleteAsync(int id)
    {
        var domain = await _context.TrackedDomains
            .FirstOrDefaultAsync(d => d.Id == id);

        if (domain != null)
        {
            domain.Delete(); // Soft delete
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Deleted tracked domain ID: {id}");
        }
    }

    public async Task<int> GetCountAsync()
    {
        return await _context.TrackedDomains
            .CountAsync(d => d.DateDeleted == null && d.IsActive);
    }
}
