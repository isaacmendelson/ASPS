using Common.Entities;
using Interface.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Business.Data.EF.Repositories;

/// <summary>
/// Repository implementation for BlacklistedPhoneNumber entity.
/// JIRA: ASPS-282
/// </summary>
public class BlacklistedPhoneNumberRepository : IBlacklistedPhoneNumberRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<BlacklistedPhoneNumberRepository> _logger;

    public BlacklistedPhoneNumberRepository(
        AppDbContext context,
        ILogger<BlacklistedPhoneNumberRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BlacklistedPhoneNumber?> GetByIdAsync(int id)
    {
        return await _context.BlacklistedPhoneNumbers
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public async Task<IEnumerable<BlacklistedPhoneNumber>> GetAllActiveAsync()
    {
        return await _context.BlacklistedPhoneNumbers
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.DateCreated)
            .ToListAsync();
    }

    public async Task<BlacklistedPhoneNumber?> GetByPhoneNumberAsync(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return null;

        var normalized = phoneNumber.Trim();

        return await _context.BlacklistedPhoneNumbers
            .FirstOrDefaultAsync(p => p.PhoneNumber == normalized && !p.IsDeleted);
    }

    public async Task<bool> IsPhoneNumberBlacklistedAsync(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        var normalized = phoneNumber.Trim();

        return await _context.BlacklistedPhoneNumbers
            .AnyAsync(p => p.PhoneNumber == normalized && !p.IsDeleted);
    }

    public async Task<int> AddAsync(BlacklistedPhoneNumber phoneNumber)
    {
        phoneNumber.DateCreated = DateTime.UtcNow;
        
        _context.BlacklistedPhoneNumbers.Add(phoneNumber);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Added blacklisted phone number: {phoneNumber.PhoneNumber} (ID: {phoneNumber.Id})");

        return phoneNumber.Id;
    }

    public async Task<int> AddRangeAsync(IEnumerable<BlacklistedPhoneNumber> phoneNumbers)
    {
        var phoneNumbersList = phoneNumbers.ToList();
        var now = DateTime.UtcNow;

        foreach (var phone in phoneNumbersList)
        {
            phone.DateCreated = now;
        }

        _context.BlacklistedPhoneNumbers.AddRange(phoneNumbersList);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Added {phoneNumbersList.Count} blacklisted phone numbers");

        return phoneNumbersList.Count;
    }

    public async Task UpdateAsync(BlacklistedPhoneNumber phoneNumber)
    {
        _context.BlacklistedPhoneNumbers.Update(phoneNumber);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Updated blacklisted phone number ID: {phoneNumber.Id}");
    }

    public async Task DeleteAsync(int id)
    {
        var phoneNumber = await _context.BlacklistedPhoneNumbers
            .FirstOrDefaultAsync(p => p.Id == id);

        if (phoneNumber != null)
        {
            phoneNumber.IsDeleted = true;
            phoneNumber.DateDeleted = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Soft deleted blacklisted phone number ID: {id}");
        }
    }

    public async Task<int> GetCountAsync()
    {
        return await _context.BlacklistedPhoneNumbers
            .CountAsync(p => !p.IsDeleted);
    }
}
