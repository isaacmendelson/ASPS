using Common.Entities;
using Common.Messaging;

namespace Business.Queries;

/// <summary>
/// Query to get all website categories with optional search and pagination.
/// JIRA: SCRUM-822
/// </summary>
public class GetAllWebsiteCategoriesQuery : Query
{
    public GetAllWebsiteCategoriesQuery()
    {
        base.QueryType = nameof(GetAllWebsiteCategoriesQuery);
    }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? Search { get; set; }
    public string? ParentId { get; set; }
}

public class GetAllWebsiteCategoriesQueryResult : QueryResult
{
    public List<WebsiteCategory> Categories { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// Query to get a single website category by name.
/// JIRA: SCRUM-822
/// </summary>
public class GetWebsiteCategoryByNameQuery : Query
{
    public GetWebsiteCategoryByNameQuery()
    {
        base.QueryType = nameof(GetWebsiteCategoryByNameQuery);
    }

    public string Name { get; set; } = string.Empty;
}

public class GetWebsiteCategoryByNameQueryResult : QueryResult
{
    public WebsiteCategory? Category { get; set; }
}

/// <summary>
/// Query to get all parent categories (for dropdown).
/// JIRA: SCRUM-822
/// </summary>
public class GetParentCategoriesQuery : Query
{
    public GetParentCategoriesQuery()
    {
        base.QueryType = nameof(GetParentCategoriesQuery);
    }
}

public class GetParentCategoriesQueryResult : QueryResult
{
    public List<WebsiteCategory> Parents { get; set; } = new();
}
