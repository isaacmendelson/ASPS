using Common.Messaging;
using Common.Models;

namespace Business.Commands;

/// <summary>
/// Command to create a new simulation
/// </summary>
public class CreateSimulationCommand : Command
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Key CreatorKey { get; set; } = new Key();
    public SimulationStep[] Steps { get; set; } = Array.Empty<SimulationStep>();
}

public class CreateSimulationCommandResult : CommandResult
{
    public Key? SimulationKey { get; set; }
}

/// <summary>
/// Command to update an existing simulation
/// </summary>
public class UpdateSimulationCommand : Command
{
    public Key SimulationKey { get; set; } = new Key();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SimulationStep[] Steps { get; set; } = Array.Empty<SimulationStep>();
}

public class UpdateSimulationCommandResult : CommandResult { }

/// <summary>
/// Command to delete a simulation (soft delete)
/// </summary>
public class DeleteSimulationCommand : Command
{
    public DeleteSimulationCommand()
    {
        CommandType = nameof(DeleteSimulationCommand);
    }
    
    public string CommandType { get; set; } = string.Empty;
    public Key SimulationKey { get; set; } = new Key();
}

public class DeleteSimulationCommandResult : CommandResult { }

/// <summary>
/// Command to run a simulation (execute the simulation steps)
/// </summary>
public class RunSimulationCommand : Command
{
    public Key SimulationKey { get; set; } = new Key();
    public Key RequestorKey { get; set; } = new Key(); // User who initiated the run
}

public class RunSimulationCommandResult : CommandResult
{
    public int TotalSteps { get; set; }
    public int ExecutedSteps { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
