using Common.Entities;
using Common.Messaging;
using Common.Models;

namespace Business.Queries;

/// <summary>
/// Query to get all simulations with optional filtering
/// </summary>
public class GetSimulationsQuery : Query
{
    public string? SearchText { get; set; }
    public Key? CreatorKey { get; set; }
}

public class GetSimulationsQueryResult : QueryResult
{
    public List<Simulation> Simulations { get; set; } = new();
}

/// <summary>
/// Query to get simulation details by key (including steps)
/// </summary>
public class GetSimulationDetailsQuery : Query
{
    public Key SimulationKey { get; set; } = new Key();
}

public class GetSimulationDetailsQueryResult : QueryResult
{
    public Simulation? Simulation { get; set; }
    public SimulationStep[] Steps { get; set; } = Array.Empty<SimulationStep>();
}

/// <summary>
/// Query to get all users for user selection in simulation steps
/// </summary>
public class GetSimulationUsersQuery : Query
{
    public string? SearchText { get; set; }
}

public class GetSimulationUsersQueryResult : QueryResult
{
    public List<SimulationUserDto> Users { get; set; } = new();
}

/// <summary>
/// DTO for user selection in simulations (lightweight)
/// </summary>
public class SimulationUserDto
{
    public Key UserKey { get; set; } = new Key();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}

/// <summary>
/// Query to search devices for autocomplete (all devices or filtered by user)
/// </summary>
public class GetSimulationDevicesQuery : Query
{
    public string? SearchText { get; set; }
    public Key? UserKey { get; set; }
}

public class GetSimulationDevicesQueryResult : QueryResult
{
    public List<SimulationDeviceDto> Devices { get; set; } = new();
}

/// <summary>
/// Query to get devices for a specific user (for device selection in simulation steps)
/// </summary>
public class GetSimulationUserDevicesQuery : Query
{
    public Key UserKey { get; set; } = new Key();
}

public class GetSimulationUserDevicesQueryResult : QueryResult
{
    public List<SimulationDeviceDto> Devices { get; set; } = new();
}

/// <summary>
/// DTO for device selection in simulations (lightweight)
/// </summary>
public class SimulationDeviceDto
{
    public Key DeviceKey { get; set; } = new Key();
    public string DeviceUid { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string MAC { get; set; } = string.Empty;
    public string? UserKeyField { get; set; }
    public string? UserFullName { get; set; }
}
