using Business.Queries;
using Interface.Repositories;
using Microsoft.Extensions.Logging;

namespace Business.Handlers;

/// <summary>
/// Query handlers for BankWebsite operations.
/// JIRA: ASPS-297
/// </summary>
public class BankWebsiteQueryHandlers
{
    private readonly IBankWebsiteRepository _repository;
    private readonly ILogger<BankWebsiteQueryHandlers> _logger;

    public BankWebsiteQueryHandlers(
        IBankWebsiteRepository repository,
        ILogger<BankWebsiteQueryHandlers> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public virtual async Task<GetAllBankWebsitesQueryResult> HandleAsync(GetAllBankWebsitesQuery query)
    {
        try
        {
            var allBankWebsites = await _repository.GetAllActiveAsync();

            // Apply IsActive filter if specified
            if (query.IsActive.HasValue)
            {
                allBankWebsites = allBankWebsites.Where(b => b.IsActive == query.IsActive.Value);
            }

            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim().ToLowerInvariant();
                allBankWebsites = allBankWebsites.Where(b =>
                    (b.Domain != null && b.Domain.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (b.BankName != null && b.BankName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (b.Country != null && b.Country.Contains(search, StringComparison.OrdinalIgnoreCase))
                );
            }

            var totalCount = allBankWebsites.Count();

            // Apply pagination
            var pagedBankWebsites = allBankWebsites
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            return new GetAllBankWebsitesQueryResult
            {
                Success = true,
                BankWebsites = pagedBankWebsites,
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bank websites");
            return new GetAllBankWebsitesQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public virtual async Task<GetBankWebsiteByIdQueryResult> HandleAsync(GetBankWebsiteByIdQuery query)
    {
        try
        {
            var bankWebsite = await _repository.GetByIdAsync(query.Id);

            return new GetBankWebsiteByIdQueryResult
            {
                Success = bankWebsite != null,
                BankWebsite = bankWebsite,
                Message = bankWebsite == null ? "Bank website not found" : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting bank website with ID {query.Id}");
            return new GetBankWebsiteByIdQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public virtual async Task<CheckDomainIsBankQueryResult> HandleAsync(CheckDomainIsBankQuery query)
    {
        try
        {
            var bankWebsite = await _repository.GetByDomainAsync(query.Domain);
            var isBank = bankWebsite != null && bankWebsite.IsActive;

            return new CheckDomainIsBankQueryResult
            {
                Success = true,
                IsBank = isBank,
                BankWebsite = bankWebsite
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking if domain {query.Domain} is a bank");
            return new CheckDomainIsBankQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
