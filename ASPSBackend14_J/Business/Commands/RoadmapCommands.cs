using Common.Messaging;

namespace Business.Commands;

// =============================================================================
// Create a new (empty) roadmap
// =============================================================================

public class CreateRoadmapCommand : Command
{
    public CreateRoadmapCommand() { CommandType = nameof(CreateRoadmapCommand); }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Optional initial JSON blob — defaults to empty {items,categories,slides}.</summary>
    public string? InitialData { get; set; }
    public string? CreatedBy { get; set; }
}

public class CreateRoadmapCommandResult : CommandResult
{
    public int RoadmapId { get; set; }
}

// =============================================================================
// Save the JSON data blob with optimistic concurrency
// =============================================================================

public class SaveRoadmapCommand : Command
{
    public SaveRoadmapCommand() { CommandType = nameof(SaveRoadmapCommand); }
    public int Id { get; set; }
    /// <summary>Version we last loaded; used as optimistic-concurrency token.</summary>
    public int ExpectedVersion { get; set; }
    /// <summary>Full JSON blob: { items, categories, slides, exportedAt }.</summary>
    public string Data { get; set; } = "{}";
    public string? UpdatedBy { get; set; }
}

public class SaveRoadmapCommandResult : CommandResult
{
    /// <summary>The new Version on success; null when a concurrent write was detected.</summary>
    public int? NewVersion { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public bool ConcurrencyConflict { get; set; }
}

// =============================================================================
// Update name/description without touching the data blob
// =============================================================================

public class UpdateRoadmapMetadataCommand : Command
{
    public UpdateRoadmapMetadataCommand() { CommandType = nameof(UpdateRoadmapMetadataCommand); }
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? UpdatedBy { get; set; }
}

public class UpdateRoadmapMetadataCommandResult : CommandResult { }

// =============================================================================
// Soft delete (archive)
// =============================================================================

public class ArchiveRoadmapCommand : Command
{
    public ArchiveRoadmapCommand() { CommandType = nameof(ArchiveRoadmapCommand); }
    public int Id { get; set; }
    public string? UpdatedBy { get; set; }
}

public class ArchiveRoadmapCommandResult : CommandResult { }
