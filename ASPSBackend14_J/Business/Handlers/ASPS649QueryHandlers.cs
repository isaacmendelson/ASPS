using Business.Queries;
using Common.Models;
using Interface.Repositories;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Business.Handlers;

/// <summary>
/// ASPS-649 — Query handlers for paged Simulations and Roadmaps (Angular Admin API).
/// </summary>
public class ASPS649QueryHandlers
{
    private readonly ISimulationRepository _simulationRepository;
    private readonly IRoadmapRepository _roadmapRepository;
    private readonly ILogger<ASPS649QueryHandlers> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true
    };

    public ASPS649QueryHandlers(
        ISimulationRepository simulationRepository,
        IRoadmapRepository roadmapRepository,
        ILogger<ASPS649QueryHandlers> logger)
    {
        _simulationRepository = simulationRepository;
        _roadmapRepository = roadmapRepository;
        _logger = logger;
    }

    // =========================================================================
    // GetAllSimulationsPagedQuery
    // =========================================================================

    public virtual async Task<GetAllSimulationsPagedQueryResult> HandleAsync(GetAllSimulationsPagedQuery query)
    {
        try
        {
            var all = (await _simulationRepository.GetAllAsync())
                .Where(s => !s.IsDeleted)
                .AsEnumerable();

            // Search
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                all = all.Where(s =>
                    s.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    s.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            // Sort
            all = (query.SortBy?.ToLowerInvariant(), query.SortDirection) switch
            {
                ("name", "desc") => all.OrderByDescending(s => s.Name),
                ("name", _)      => all.OrderBy(s => s.Name),
                ("datecreated", "desc") => all.OrderByDescending(s => s.DateCreated),
                ("datecreated", _)      => all.OrderBy(s => s.DateCreated),
                _ => all.OrderByDescending(s => s.DateCreated)
            };

            var list = all.ToList();
            var totalCount = list.Count;
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            var paged = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new GetAllSimulationsPagedQueryResult
            {
                Success = true,
                Result = new PagedResult<SimulationDto>
                {
                    Items = paged.Select(MapToDto).ToList(),
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged simulations");
            return new GetAllSimulationsPagedQueryResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    // =========================================================================
    // GetSimulationByKeyFieldQuery
    // =========================================================================

    public virtual async Task<GetSimulationByKeyFieldQueryResult> HandleAsync(GetSimulationByKeyFieldQuery query)
    {
        try
        {
            var key = new Key("Simulation", query.KeyField);
            var simulation = await _simulationRepository.GetByKeyAsync(key);

            if (simulation == null || simulation.IsDeleted)
            {
                return new GetSimulationByKeyFieldQueryResult
                {
                    Success = false,
                    Message = "Simulation not found"
                };
            }

            return new GetSimulationByKeyFieldQueryResult
            {
                Success = true,
                Simulation = MapToDto(simulation)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting simulation by key {KeyField}", query.KeyField);
            return new GetSimulationByKeyFieldQueryResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    // =========================================================================
    // GetAllRoadmapsPagedQuery
    // =========================================================================

    public virtual async Task<GetAllRoadmapsPagedQueryResult> HandleAsync(GetAllRoadmapsPagedQuery query)
    {
        try
        {
            var all = (await _roadmapRepository.ListAsync(query.IncludeArchived)).AsEnumerable();

            // Search
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                all = all.Where(r =>
                    r.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (r.Description != null && r.Description.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            // Sort
            all = (query.SortBy?.ToLowerInvariant(), query.SortDirection) switch
            {
                ("name", "desc") => all.OrderByDescending(r => r.Name),
                ("name", _)      => all.OrderBy(r => r.Name),
                ("datecreated", "desc") => all.OrderByDescending(r => r.DateCreated),
                ("datecreated", _)      => all.OrderBy(r => r.DateCreated),
                ("lastupdatedat", "desc") => all.OrderByDescending(r => r.LastUpdatedAt),
                ("lastupdatedat", _)      => all.OrderBy(r => r.LastUpdatedAt),
                _ => all.OrderByDescending(r => r.LastUpdatedAt ?? r.DateCreated)
            };

            var list = all.ToList();
            var totalCount = list.Count;
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            var paged = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new GetAllRoadmapsPagedQueryResult
            {
                Success = true,
                Result = new PagedResult<RoadmapDto>
                {
                    Items = paged.Select(r => new RoadmapDto
                    {
                        Id = r.Id,
                        Name = r.Name,
                        Description = r.Description,
                        Version = r.Version,
                        DateCreated = r.DateCreated,
                        LastUpdatedAt = r.LastUpdatedAt,
                        CreatedBy = r.CreatedBy,
                        LastUpdatedBy = r.LastUpdatedBy,
                        IsArchived = r.IsArchived
                    }).ToList(),
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged roadmaps");
            return new GetAllRoadmapsPagedQueryResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    private SimulationDto MapToDto(Common.Entities.Simulation s)
    {
        List<SimulationStep> steps = new();
        if (!string.IsNullOrWhiteSpace(s.SimulationStepsJson))
        {
            try
            {
                steps = JsonSerializer.Deserialize<List<SimulationStep>>(s.SimulationStepsJson, _jsonOptions)
                    ?? new List<SimulationStep>();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize SimulationStepsJson for simulation {KeyField}", s.KeyField);
            }
        }

        return new SimulationDto
        {
            Key = s.Key,
            Name = s.Name,
            Description = s.Description,
            CreatorKeyField = s.CreatorKeyField,
            Steps = steps,
            IsDeleted = s.IsDeleted,
            IsDisabled = s.IsDisabled,
            DateCreated = s.DateCreated,
            DateModified = s.DateModified
        };
    }
}
