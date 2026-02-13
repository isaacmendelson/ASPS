using Common.Entities;
using Interface.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Business.Data.EF.Repositories;

public class KnownPhishingWebsiteRepository : IKnownPhishingWebsiteRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<KnownPhishingWebsiteRepository> _logger;

    public KnownPhishingWebsiteRepository(
        AppDbContext context,
        ILogger<KnownPhishingWebsiteRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<KnownPhishingWebsite?> GetByIdAsync(int id)
    {
        return await _context.KnownPhishingWebsites
            .FirstOrDefaultAsync(w => w.Id == id && w.DateDeleted == null);
    }

    public async Task<IEnumerable<KnownPhishingWebsite>> GetAllActiveAsync()
    {
        return await _context.KnownPhishingWebsites
            .Where(w => w.DateDeleted == null)
            .ToListAsync();
    }

    public async Task<KnownPhishingWebsite?> GetByUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        return await _context.KnownPhishingWebsites
            .FirstOrDefaultAsync(w => w.Url == url && w.DateDeleted == null);
    }

    public async Task<IEnumerable<KnownPhishingWebsite>> GetByDomainAsync(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return Enumerable.Empty<KnownPhishingWebsite>();

        var normalizedDomain = domain.ToLowerInvariant();
        
        return await _context.KnownPhishingWebsites
            .Where(w => w.Domain == normalizedDomain && w.DateDeleted == null)
            .ToListAsync();
    }

    public async Task<bool> IsPhishingUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return await _context.KnownPhishingWebsites
            .AnyAsync(w => w.Url == url && w.DateDeleted == null);
    }

    public async Task<bool> IsPhishingDomainAsync(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return false;

        var normalizedDomain = domain.ToLowerInvariant();
        
        return await _context.KnownPhishingWebsites
            .AnyAsync(w => w.Domain == normalizedDomain && w.DateDeleted == null);
    }

    public async Task<int> AddAsync(KnownPhishingWebsite website)
    {
        _context.KnownPhishingWebsites.Add(website);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation($"Added phishing website: {website.Url} (ID: {website.Id})");
        
        return website.Id;
    }

    public async Task<int> AddRangeAsync(IEnumerable<KnownPhishingWebsite> websites)
    {
        var websitesList = websites.ToList();
        
        _context.KnownPhishingWebsites.AddRange(websitesList);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation($"Added {websitesList.Count} phishing websites");
        
        return websitesList.Count;
    }

    public async Task UpdateAsync(KnownPhishingWebsite website)
    {
        _context.KnownPhishingWebsites.Update(website);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation($"Updated phishing website ID: {website.Id}");
    }

    public async Task DeleteAsync(int id)
    {
        var website = await _context.KnownPhishingWebsites
            .FirstOrDefaultAsync(w => w.Id == id);
        
        if (website != null)
        {
            website.Delete(); // Soft delete
            await _context.SaveChangesAsync();
            
            _logger.LogInformation($"Deleted phishing website ID: {id}");
        }
    }

    public async Task<int> GetCountAsync()
    {
        return await _context.KnownPhishingWebsites
            .CountAsync(w => w.DateDeleted == null);
    }
}
