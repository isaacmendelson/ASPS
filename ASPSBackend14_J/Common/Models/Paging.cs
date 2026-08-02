namespace Common.Models;

/// <summary>
/// Query parameters for server-side paged list requests.
/// Call <see cref="Normalize"/> in every controller action before passing to the Business layer.
/// </summary>
public class PagedRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string SortDirection { get; set; } = "asc";

    /// <summary>
    /// Clamps Page and PageSize to safe bounds and enforces valid SortDirection values.
    /// </summary>
    public void Normalize()
    {
        if (Page < 1) Page = 1;
        if (PageSize < 1) PageSize = 25;
        if (PageSize > 100) PageSize = 100;
        if (SortDirection != "asc" && SortDirection != "desc")
            SortDirection = "asc";
    }
}

/// <summary>
/// Standard envelope for server-side paged list responses.
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }

    /// <summary>Computed from TotalCount / PageSize (ceiling division).</summary>
    public int TotalPages => PageSize > 0
        ? (int)Math.Ceiling(TotalCount / (double)PageSize)
        : 0;
}
