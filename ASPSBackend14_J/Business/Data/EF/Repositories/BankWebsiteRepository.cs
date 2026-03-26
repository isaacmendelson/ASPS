using Common.Entities;
using Interface.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Business.Data.EF.Repositories;

/// <summary>
/// Repository implementation for BankWebsite entity.
/// JIRA: ASPS-297
/// </summary>
public class BankWebsiteRepository : IBankWebsiteRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<BankWebsiteRepository> _logger;

    public BankWebsiteRepository(
        AppDbContext context,
        ILogger<BankWebsiteRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BankWebsite?> GetByIdAsync(int id)
    {
        return await _context.BankWebsites
            .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);
    }

    public async Task<IEnumerable<BankWebsite>> GetAllActiveAsync()
    {
        return await _context.BankWebsites
            .Where(b => !b.IsDeleted && b.IsActive)
            .OrderBy(b => b.BankName)
            .ToListAsync();
    }

    public async Task<BankWebsite?> GetByDomainAsync(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return null;

        var normalized = domain.ToLowerInvariant().Trim();

        return await _context.BankWebsites
            .FirstOrDefaultAsync(b => b.Domain == normalized && !b.IsDeleted);
    }

    public async Task<IEnumerable<BankWebsite>> GetByCountryAsync(string country)
    {
        if (string.IsNullOrWhiteSpace(country))
            return Enumerable.Empty<BankWebsite>();

        var normalized = country.Trim();

        return await _context.BankWebsites
            .Where(b => b.Country == normalized && !b.IsDeleted && b.IsActive)
            .OrderBy(b => b.BankName)
            .ToListAsync();
    }

    public async Task<bool> IsBankDomainAsync(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return false;

        var normalized = domain.ToLowerInvariant().Trim();

        return await _context.BankWebsites
            .AnyAsync(b => b.Domain == normalized && !b.IsDeleted && b.IsActive);
    }

    public async Task<int> AddAsync(BankWebsite bankWebsite)
    {
        bankWebsite.DateCreated = DateTime.UtcNow;
        bankWebsite.Domain = bankWebsite.Domain.ToLowerInvariant().Trim();

        _context.BankWebsites.Add(bankWebsite);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Added bank website: {bankWebsite.BankName} - {bankWebsite.Domain} (ID: {bankWebsite.Id})");

        return bankWebsite.Id;
    }

    public async Task<int> AddRangeAsync(IEnumerable<BankWebsite> bankWebsites)
    {
        var bankWebsitesList = bankWebsites.ToList();
        var now = DateTime.UtcNow;

        foreach (var bank in bankWebsitesList)
        {
            bank.DateCreated = now;
            bank.Domain = bank.Domain.ToLowerInvariant().Trim();
        }

        _context.BankWebsites.AddRange(bankWebsitesList);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Added {bankWebsitesList.Count} bank websites");

        return bankWebsitesList.Count;
    }

    public async Task UpdateAsync(BankWebsite bankWebsite)
    {
        bankWebsite.DateModified = DateTime.UtcNow;
        bankWebsite.Domain = bankWebsite.Domain.ToLowerInvariant().Trim();

        _context.BankWebsites.Update(bankWebsite);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Updated bank website ID: {bankWebsite.Id}");
    }

    public async Task DeleteAsync(int id)
    {
        var bankWebsite = await _context.BankWebsites
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bankWebsite != null)
        {
            bankWebsite.IsDeleted = true;
            bankWebsite.DateDeleted = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Soft deleted bank website ID: {id}");
        }
    }

    public async Task<int> GetCountAsync()
    {
        return await _context.BankWebsites
            .CountAsync(b => !b.IsDeleted);
    }
}
