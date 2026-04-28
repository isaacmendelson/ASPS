using Business.Commands;
using Common.Entities;
using Interface.Repositories;
using Microsoft.Extensions.Logging;

namespace Business.Handlers;

public class RoadmapCommandHandlers
{
    private const string EmptyData = "{\"items\":[],\"categories\":[],\"slides\":[]}";

    private readonly IRoadmapRepository _repository;
    private readonly ILogger<RoadmapCommandHandlers> _logger;

    public RoadmapCommandHandlers(IRoadmapRepository repository, ILogger<RoadmapCommandHandlers> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public virtual async Task<CreateRoadmapCommandResult> HandleAsync(CreateRoadmapCommand cmd)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
            {
                return new CreateRoadmapCommandResult { Success = false, Message = "Name is required" };
            }
            var roadmap = new Roadmap
            {
                Name = cmd.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(cmd.Description) ? null : cmd.Description.Trim(),
                Data = string.IsNullOrWhiteSpace(cmd.InitialData) ? EmptyData : cmd.InitialData,
                Version = 1,
                CreatedBy = cmd.CreatedBy,
                LastUpdatedBy = cmd.CreatedBy,
                LastUpdatedAt = DateTime.UtcNow,
            };
            var id = await _repository.AddAsync(roadmap);
            return new CreateRoadmapCommandResult { Success = true, RoadmapId = id };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating roadmap");
            return new CreateRoadmapCommandResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    public virtual async Task<SaveRoadmapCommandResult> HandleAsync(SaveRoadmapCommand cmd)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(cmd.Data))
            {
                return new SaveRoadmapCommandResult { Success = false, Message = "Data is required" };
            }
            var newVersion = await _repository.UpdateAsync(cmd.Id, cmd.Data, cmd.ExpectedVersion, cmd.UpdatedBy);
            if (newVersion is null)
            {
                // Could be "not found" or "concurrency conflict". Disambiguate.
                var current = await _repository.GetByIdAsync(cmd.Id);
                if (current is null)
                {
                    return new SaveRoadmapCommandResult { Success = false, Message = "Roadmap not found" };
                }
                return new SaveRoadmapCommandResult
                {
                    Success = false,
                    ConcurrencyConflict = true,
                    NewVersion = current.Version,
                    LastUpdatedAt = current.LastUpdatedAt,
                    Message = $"Concurrency conflict: server is at version {current.Version}, you sent {cmd.ExpectedVersion}",
                };
            }
            return new SaveRoadmapCommandResult
            {
                Success = true,
                NewVersion = newVersion,
                LastUpdatedAt = DateTime.UtcNow,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving roadmap {Id}", cmd.Id);
            return new SaveRoadmapCommandResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    public virtual async Task<UpdateRoadmapMetadataCommandResult> HandleAsync(UpdateRoadmapMetadataCommand cmd)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
            {
                return new UpdateRoadmapMetadataCommandResult { Success = false, Message = "Name is required" };
            }
            var ok = await _repository.UpdateMetadataAsync(cmd.Id, cmd.Name.Trim(),
                string.IsNullOrWhiteSpace(cmd.Description) ? null : cmd.Description.Trim(), cmd.UpdatedBy);
            if (!ok)
            {
                return new UpdateRoadmapMetadataCommandResult { Success = false, Message = "Roadmap not found" };
            }
            return new UpdateRoadmapMetadataCommandResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating roadmap metadata {Id}", cmd.Id);
            return new UpdateRoadmapMetadataCommandResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    public virtual async Task<ArchiveRoadmapCommandResult> HandleAsync(ArchiveRoadmapCommand cmd)
    {
        try
        {
            var ok = await _repository.ArchiveAsync(cmd.Id, cmd.UpdatedBy);
            if (!ok)
            {
                return new ArchiveRoadmapCommandResult { Success = false, Message = "Roadmap not found" };
            }
            return new ArchiveRoadmapCommandResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving roadmap {Id}", cmd.Id);
            return new ArchiveRoadmapCommandResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }
}
