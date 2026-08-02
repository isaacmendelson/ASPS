namespace Common.Messaging;

/// <summary>
/// Base class for CQRS queries that return server-side paged results.
/// Concrete query types inherit from this class and add domain-specific filters.
/// </summary>
public abstract class PagedQuery : Query
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string SortDirection { get; set; } = "asc";
}
