using Common.Entities;
using Interface.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Business.Data.EF.Repositories;

public class SafeDomainRepository : ISafeDomainRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<SafeDomainRepository> _logger;

    public SafeDomainRepository(
        AppDbContext context,
        ILogger<SafeDomainRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<SafeDomain>> GetAllActiveAsync()
    {
        return await _context.SafeDomains
            .Where(d => !d.IsDeleted)
            .ToListAsync();
    }

    public async Task<bool> IsSafeDomainAsync(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return false;

        var normalizedDomain = domain.ToLowerInvariant();

        return await _context.SafeDomains
            .AnyAsync(d => d.Domain == normalizedDomain && !d.IsDeleted);
    }
}
