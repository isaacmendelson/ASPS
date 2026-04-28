using Common.Entities;

namespace Interface.Repositories;

/// <summary>
/// Repository for Roadmap entity (JSON-blob design).
/// </summary>
public interface IRoadmapRepository
{
    Task<Roadmap?> GetByIdAsync(int id);
    Task<IEnumerable<Roadmap>> ListAsync(bool includeArchived = false);
    Task<int> AddAsync(Roadmap roadmap);

    /// <summary>
    /// Optimistic concurrency save: only updates if `expectedVersion` matches the
    /// row's current Version. Returns the new Version on success, or null if a
    /// concurrent write was detected.
    /// </summary>
    Task<int?> UpdateAsync(int id, string data, int expectedVersion, string? updatedBy);

    /// <summary>Update a roadmap's name/description without touching the data blob.</summary>
    Task<bool> UpdateMetadataAsync(int id, string name, string? description, string? updatedBy);

    Task<bool> ArchiveAsync(int id, string? updatedBy);
}
