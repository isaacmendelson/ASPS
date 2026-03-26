using Business.Queries;
using Interface.Repositories;
using Microsoft.Extensions.Logging;

namespace Business.Handlers;

/// <summary>
/// Query handlers for BlacklistedPhoneNumber operations.
/// JIRA: ASPS-282
/// </summary>
public class BlacklistedPhoneNumberQueryHandlers
{
    private readonly IBlacklistedPhoneNumberRepository _repository;
    private readonly ILogger<BlacklistedPhoneNumberQueryHandlers> _logger;

    public BlacklistedPhoneNumberQueryHandlers(
        IBlacklistedPhoneNumberRepository repository,
        ILogger<BlacklistedPhoneNumberQueryHandlers> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public virtual async Task<GetAllBlacklistedPhoneNumbersQueryResult> HandleAsync(GetAllBlacklistedPhoneNumbersQuery query)
    {
        try
        {
            var allPhoneNumbers = await _repository.GetAllActiveAsync();

            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim().ToLowerInvariant();
                allPhoneNumbers = allPhoneNumbers.Where(p =>
                    (p.PhoneNumber != null && p.PhoneNumber.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (p.Source != null && p.Source.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (p.Notes != null && p.Notes.Contains(search, StringComparison.OrdinalIgnoreCase))
                );
            }

            var totalCount = allPhoneNumbers.Count();

            // Apply pagination
            var pagedPhoneNumbers = allPhoneNumbers
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            return new GetAllBlacklistedPhoneNumbersQueryResult
            {
                Success = true,
                PhoneNumbers = pagedPhoneNumbers,
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting blacklisted phone numbers");
            return new GetAllBlacklistedPhoneNumbersQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public virtual async Task<GetBlacklistedPhoneNumberByIdQueryResult> HandleAsync(GetBlacklistedPhoneNumberByIdQuery query)
    {
        try
        {
            var phoneNumber = await _repository.GetByIdAsync(query.Id);

            return new GetBlacklistedPhoneNumberByIdQueryResult
            {
                Success = phoneNumber != null,
                PhoneNumber = phoneNumber,
                Message = phoneNumber == null ? "Phone number not found" : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting blacklisted phone number with ID {query.Id}");
            return new GetBlacklistedPhoneNumberByIdQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public virtual async Task<CheckPhoneNumberBlacklistedQueryResult> HandleAsync(CheckPhoneNumberBlacklistedQuery query)
    {
        try
        {
            var phoneNumber = await _repository.GetByPhoneNumberAsync(query.PhoneNumber);
            var isBlacklisted = phoneNumber != null;

            return new CheckPhoneNumberBlacklistedQueryResult
            {
                Success = true,
                IsBlacklisted = isBlacklisted,
                PhoneNumber = phoneNumber
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking if phone number {query.PhoneNumber} is blacklisted");
            return new CheckPhoneNumberBlacklistedQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
