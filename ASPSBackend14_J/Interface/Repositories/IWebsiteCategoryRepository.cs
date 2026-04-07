using Common.Entities;

namespace Interface.Repositories;

/// <summary>
/// Repository for managing website categories.
/// JIRA: SCRUM-819
/// </summary>
public interface IWebsiteCategoryRepository
{
    Task<IEnumerable<WebsiteCategory>> GetAllAsync();
    Task<WebsiteCategory?> GetByNameAsync(string name);
    Task<int> AddAsync(WebsiteCategory category);
    Task UpdateAsync(WebsiteCategory category);
    Task<bool> ExistsAsync(string name);
}
