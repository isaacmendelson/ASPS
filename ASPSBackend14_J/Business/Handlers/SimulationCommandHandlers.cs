using Business.Commands;
using Business.Services;
using Common.Entities;
using Common.Models;
using Interface.Repositories;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Business.Handlers;

public class SimulationCommandHandlers
{
    private readonly ISimulationRepository _simulationRepository;
    private readonly IUserRepository _userRepository;
    private readonly SimulationRunner _simulationRunner;

    public SimulationCommandHandlers(
        ISimulationRepository simulationRepository,
        IUserRepository userRepository,
        SimulationRunner simulationRunner)
    {
        _simulationRepository = simulationRepository;
        _userRepository = userRepository;
        _simulationRunner = simulationRunner;
    }

    /// <summary>
    /// Handle CreateSimulationCommand
    /// </summary>
    public virtual async Task<CreateSimulationCommandResult> HandleAsync(CreateSimulationCommand command)
    {
        try
        {
            // Validate creator exists
            var creator = await _userRepository.GetByKeyAsync(command.CreatorKey);
            if (creator == null)
            {
                return new CreateSimulationCommandResult
                {
                    Success = false,
                    Message = "Creator user not found"
                };
            }

            // Create simulation entity
            var simulation = new Simulation
            {
                KeyField = Guid.NewGuid().ToString(),
                Name = command.Name,
                Description = command.Description,
                CreatorKeyField = Entity.GetDbKey(command.CreatorKey),
                SimulationStepsJson = command.Steps.Length > 0 
                    ? JsonSerializer.Serialize(command.Steps) 
                    : null,
                DateCreated = DateTime.UtcNow
            };

            var created = await _simulationRepository.AddAsync(simulation);

            return new CreateSimulationCommandResult
            {
                Success = true,
                Message = "Simulation created successfully",
                SimulationKey = created.Key
            };
        }
        catch (Exception ex)
        {
            return new CreateSimulationCommandResult
            {
                Success = false,
                Message = $"Error creating simulation: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Handle UpdateSimulationCommand
    /// </summary>
    public virtual async Task<UpdateSimulationCommandResult> HandleAsync(UpdateSimulationCommand command)
    {
        try
        {
            var simulation = await _simulationRepository.GetByKeyAsync(command.SimulationKey);
            if (simulation == null)
            {
                return new UpdateSimulationCommandResult
                {
                    Success = false,
                    Message = "Simulation not found"
                };
            }

            // Update fields
            simulation.Name = command.Name;
            simulation.Description = command.Description;
            simulation.SimulationStepsJson = command.Steps.Length > 0 
                ? JsonSerializer.Serialize(command.Steps) 
                : null;
            simulation.DateModified = DateTime.UtcNow;

            await _simulationRepository.UpdateAsync(simulation);

            return new UpdateSimulationCommandResult
            {
                Success = true,
                Message = "Simulation updated successfully"
            };
        }
        catch (Exception ex)
        {
            return new UpdateSimulationCommandResult
            {
                Success = false,
                Message = $"Error updating simulation: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Handle DeleteSimulationCommand (soft delete)
    /// </summary>
    public virtual async Task<DeleteSimulationCommandResult> HandleAsync(DeleteSimulationCommand command)
    {
        try
        {
            await _simulationRepository.DeleteAsync(command.SimulationKey);

            return new DeleteSimulationCommandResult
            {
                Success = true,
                Message = "Simulation deleted successfully"
            };
        }
        catch (Exception ex)
        {
            return new DeleteSimulationCommandResult
            {
                Success = false,
                Message = $"Error deleting simulation: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Handle RunSimulationCommand
    /// Note: This is a placeholder. The actual execution logic will be in SimulationRunner service.
    /// This handler validates the simulation and queues it for execution.
    /// </summary>
    public virtual async Task<RunSimulationCommandResult> HandleAsync(RunSimulationCommand command)
    {
        try
        {
            var simulation = await _simulationRepository.GetByKeyAsync(command.SimulationKey);
            if (simulation == null)
            {
                return new RunSimulationCommandResult
                {
                    Success = false,
                    Message = "Simulation not found"
                };
            }

            // Deserialize steps
            SimulationStep[]? steps = null;
            if (!string.IsNullOrWhiteSpace(simulation.SimulationStepsJson))
            {
                var options = new JsonSerializerOptions
                {
                    Converters = { new JsonStringEnumConverter() },
                    PropertyNameCaseInsensitive = true
                };
                steps = JsonSerializer.Deserialize<SimulationStep[]>(simulation.SimulationStepsJson, options);
            }

            if (steps == null || steps.Length == 0)
            {
                return new RunSimulationCommandResult
                {
                    Success = false,
                    Message = "Simulation has no steps to execute"
                };
            }

            // Queue simulation for background execution
            _simulationRunner.QueueSimulation(command.SimulationKey, command.RequestorKey);

            return new RunSimulationCommandResult
            {
                Success = true,
                Message = "Simulation queued for execution",
                TotalSteps = steps.Length,
                ExecutedSteps = 0,
                StartedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new RunSimulationCommandResult
            {
                Success = false,
                Message = $"Error running simulation: {ex.Message}"
            };
        }
    }
}
