using Business.Queries;
using Interface.Repositories;
using Microsoft.Extensions.Logging;

namespace Business.Handlers;

public class RoadmapQueryHandlers
{
    private readonly IRoadmapRepository _repository;
    private readonly ILogger<RoadmapQueryHandlers> _logger;

    public RoadmapQueryHandlers(IRoadmapRepository repository, ILogger<RoadmapQueryHandlers> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public virtual async Task<GetRoadmapByIdQueryResult> HandleAsync(GetRoadmapByIdQuery query)
    {
        try
        {
            var r = await _repository.GetByIdAsync(query.Id);
            if (r is null)
            {
                return new GetRoadmapByIdQueryResult { Success = false, Message = "Roadmap not found" };
            }
            return new GetRoadmapByIdQueryResult
            {
                Success = true,
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                Data = r.Data,
                Version = r.Version,
                DateCreated = r.DateCreated,
                LastUpdatedAt = r.LastUpdatedAt,
                CreatedBy = r.CreatedBy,
                LastUpdatedBy = r.LastUpdatedBy,
                IsArchived = r.IsArchived,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting roadmap {Id}", query.Id);
            return new GetRoadmapByIdQueryResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    public virtual async Task<ListRoadmapsQueryResult> HandleAsync(ListRoadmapsQuery query)
    {
        try
        {
            var roadmaps = await _repository.ListAsync(query.IncludeArchived);
            return new ListRoadmapsQueryResult
            {
                Success = true,
                Roadmaps = roadmaps.Select(r => new RoadmapListItem
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    Version = r.Version,
                    DateCreated = r.DateCreated,
                    LastUpdatedAt = r.LastUpdatedAt,
                    LastUpdatedBy = r.LastUpdatedBy,
                    IsArchived = r.IsArchived,
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing roadmaps");
            return new ListRoadmapsQueryResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }
}
