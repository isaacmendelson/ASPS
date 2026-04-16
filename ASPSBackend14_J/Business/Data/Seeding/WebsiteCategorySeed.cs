using Common.Entities;
using Business.Data.EF;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Business.Data.Seeding;

/// <summary>
/// Seeds the initial 58 website categories from category_patterns.json
/// JIRA: SCRUM-819
/// </summary>
public class WebsiteCategorySeed
{
    private readonly AppDbContext _context;
    private readonly ILogger<WebsiteCategorySeed> _logger;

    public WebsiteCategorySeed(AppDbContext context, ILogger<WebsiteCategorySeed> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            // Check if categories already exist
            var existingCount = _context.WebsiteCategories.Count(c => c.DateDeleted == null);
            if (existingCount > 0)
            {
                _logger.LogInformation("WebsiteCategories already seeded ({Count} categories exist). Skipping.", existingCount);
                return;
            }

            var categoriesJson = File.ReadAllText("/root/.openclaw/workspace-ceo/asps/Analyzers/basic-url-analyzer/config/category_patterns.json");
            var jsonDoc = JsonDocument.Parse(categoriesJson);
            var categoriesElement = jsonDoc.RootElement.GetProperty("categories");

            var categories = new List<WebsiteCategory>();
            
            // First pass: Create group parent categories
            var groups = new Dictionary<string, WebsiteCategory>
            {
                { "financial", new WebsiteCategory("Financial", null, "seed_import") },
                { "shopping", new WebsiteCategory("Shopping", null, "seed_import") },
                { "government", new WebsiteCategory("Government", null, "seed_import") },
                { "health", new WebsiteCategory("Health", null, "seed_import") },
                { "education", new WebsiteCategory("Education", null, "seed_import") },
                { "entertainment", new WebsiteCategory("Entertainment", null, "seed_import") },
                { "media", new WebsiteCategory("Media", null, "seed_import") },
                { "services", new WebsiteCategory("Services", null, "seed_import") },
                { "technology", new WebsiteCategory("Technology", null, "seed_import") },
                { "other", new WebsiteCategory("Other", null, "seed_import") }
            };

            foreach (var group in groups.Values)
            {
                categories.Add(group);
            }

            // Second pass: Create category entries with parent references
            foreach (var categoryProp in categoriesElement.EnumerateObject())
            {
                var categoryKey = categoryProp.Name; // e.g., "banking"
                var categoryData = categoryProp.Value;

                var nameEn = categoryData.GetProperty("name_en").GetString() ?? categoryKey;
                var group = categoryData.GetProperty("group").GetString() ?? "other";

                if (!groups.ContainsKey(group))
                {
                    _logger.LogWarning("Unknown group '{Group}' for category '{Category}'. Using 'other'.", group, categoryKey);
                    group = "other";
                }

                var parentCategory = groups[group];
                var category = new WebsiteCategory(nameEn, parentCategory.KeyField, "seed_import");
                categories.Add(category);
            }

            await _context.WebsiteCategories.AddRangeAsync(categories);
            var savedCount = await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully seeded {Count} website categories.", savedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed WebsiteCategories.");
            throw;
        }
    }
}
