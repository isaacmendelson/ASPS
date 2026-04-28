using Common.Messaging;

namespace Business.Queries;

// =============================================================================
// Get a single roadmap by ID (full data blob)
// =============================================================================

public class GetRoadmapByIdQuery : Query
{
    public GetRoadmapByIdQuery() { QueryType = nameof(GetRoadmapByIdQuery); }
    public int Id { get; set; }
}

public class GetRoadmapByIdQueryResult : QueryResult
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Full JSON blob: { items, categories, slides, exportedAt }.</summary>
    public string Data { get; set; } = "{}";
    public int Version { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? LastUpdatedBy { get; set; }
    public bool IsArchived { get; set; }
}

// =============================================================================
// List roadmaps (for index/picker - excludes archived by default)
// =============================================================================

public class ListRoadmapsQuery : Query
{
    public ListRoadmapsQuery() { QueryType = nameof(ListRoadmapsQuery); }
    public bool IncludeArchived { get; set; } = false;
}

public class RoadmapListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Version { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public string? LastUpdatedBy { get; set; }
    public bool IsArchived { get; set; }
}

public class ListRoadmapsQueryResult : QueryResult
{
    public List<RoadmapListItem> Roadmaps { get; set; } = new();
}
