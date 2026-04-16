using Business.Queries;
using Interface.Repositories;
using Microsoft.Extensions.Logging;

namespace Business.Handlers;

/// <summary>
/// Query handlers for WebsiteCategory operations.
/// JIRA: SCRUM-822
/// </summary>
public class WebsiteCategoryQueryHandlers
{
    private readonly IWebsiteCategoryRepository _repository;
    private readonly ILogger<WebsiteCategoryQueryHandlers> _logger;

    public WebsiteCategoryQueryHandlers(
        IWebsiteCategoryRepository repository,
        ILogger<WebsiteCategoryQueryHandlers> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public virtual async Task<GetAllWebsiteCategoriesQueryResult> HandleAsync(GetAllWebsiteCategoriesQuery query)
    {
        try
        {
            var allCategories = await _repository.GetAllQueryableAsync();

            // Apply ParentId filter if specified
            if (!string.IsNullOrWhiteSpace(query.ParentId))
            {
                allCategories = allCategories.Where(c => c.ParentId == query.ParentId);
            }

            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim().ToLowerInvariant();
                allCategories = allCategories.Where(c =>
                    (c.Name != null && c.Name.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (c.Source != null && c.Source.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (c.Parent?.Name != null && c.Parent.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                );
            }

            var totalCount = allCategories.Count();

            // Apply pagination
            var pagedCategories = allCategories
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            return new GetAllWebsiteCategoriesQueryResult
            {
                Success = true,
                Categories = pagedCategories,
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting website categories");
            return new GetAllWebsiteCategoriesQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public virtual async Task<GetWebsiteCategoryByNameQueryResult> HandleAsync(GetWebsiteCategoryByNameQuery query)
    {
        try
        {
            var category = await _repository.GetByNameAsync(query.Name);

            return new GetWebsiteCategoryByNameQueryResult
            {
                Success = category != null,
                Category = category,
                Message = category == null ? "Website category not found" : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting website category with name {query.Name}");
            return new GetWebsiteCategoryByNameQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public virtual async Task<GetParentCategoriesQueryResult> HandleAsync(GetParentCategoriesQuery query)
    {
        try
        {
            var allCategories = await _repository.GetAllAsync();
            // Get unique parent categories (categories that have at least one child or are top-level)
            var parents = allCategories
                .Where(c => string.IsNullOrEmpty(c.ParentId) || allCategories.Any(child => child.ParentId == c.Name))
                .OrderBy(c => c.Name)
                .ToList();

            return new GetParentCategoriesQueryResult
            {
                Success = true,
                Parents = parents
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting parent categories");
            return new GetParentCategoriesQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
