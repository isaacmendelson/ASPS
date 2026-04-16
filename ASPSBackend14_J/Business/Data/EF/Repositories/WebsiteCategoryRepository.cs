using Common.Entities;
using Interface.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Business.Data.EF.Repositories;

/// <summary>
/// Repository implementation for managing website categories.
/// JIRA: SCRUM-819
/// </summary>
public class WebsiteCategoryRepository : IWebsiteCategoryRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<WebsiteCategoryRepository> _logger;

    public WebsiteCategoryRepository(
        AppDbContext context,
        ILogger<WebsiteCategoryRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<WebsiteCategory>> GetAllAsync()
    {
        return await _context.WebsiteCategories
            .Where(c => c.DateDeleted == null)
            .Include(c => c.Parent)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IQueryable<WebsiteCategory>> GetAllQueryableAsync()
    {
        return _context.WebsiteCategories
            .Where(c => c.DateDeleted == null)
            .Include(c => c.Parent)
            .OrderBy(c => c.Name);
    }

    public async Task<WebsiteCategory?> GetByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var normalizedName = name.ToLowerInvariant();

        return await _context.WebsiteCategories
            .Include(c => c.Parent)
            .FirstOrDefaultAsync(c => c.Name.ToLower() == normalizedName && c.DateDeleted == null);
    }

    public async Task<int> AddAsync(WebsiteCategory category)
    {
        if (category == null)
            throw new ArgumentNullException(nameof(category));

        await _context.WebsiteCategories.AddAsync(category);
        return await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(WebsiteCategory category)
    {
        if (category == null)
            throw new ArgumentNullException(nameof(category));

        _context.WebsiteCategories.Update(category);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var normalizedName = name.ToLowerInvariant();

        return await _context.WebsiteCategories
            .AnyAsync(c => c.Name.ToLower() == normalizedName && c.DateDeleted == null);
    }
}
