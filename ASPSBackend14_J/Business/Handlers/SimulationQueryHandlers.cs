using Business.Queries;
using Common.Models;
using Interface.Repositories;
using System.Text.Json;

namespace Business.Handlers;

public class SimulationQueryHandlers
{
    private readonly ISimulationRepository _simulationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserDeviceRepository _userDeviceRepository;

    public SimulationQueryHandlers(
        ISimulationRepository simulationRepository,
        IUserRepository userRepository,
        IUserDeviceRepository userDeviceRepository)
    {
        _simulationRepository = simulationRepository;
        _userRepository = userRepository;
        _userDeviceRepository = userDeviceRepository;
    }

    /// <summary>
    /// Handle GetSimulationsQuery - get all simulations with optional filtering
    /// </summary>
    public virtual async Task<GetSimulationsQueryResult> HandleAsync(GetSimulationsQuery query)
    {
        try
        {
            IEnumerable<Common.Entities.Simulation> simulations;

            if (query.CreatorKey != null)
            {
                // Filter by creator
                simulations = await _simulationRepository.GetByCreatorKeyAsync(query.CreatorKey);
            }
            else if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                // Search by text
                simulations = await _simulationRepository.SearchAsync(query.SearchText);
            }
            else
            {
                // Get all
                simulations = await _simulationRepository.GetAllAsync();
            }

            return new GetSimulationsQueryResult
            {
                Success = true,
                Simulations = simulations.ToList()
            };
        }
        catch (Exception ex)
        {
            return new GetSimulationsQueryResult
            {
                Success = false,
                Message = $"Error retrieving simulations: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Handle GetSimulationDetailsQuery - get simulation with deserialized steps
    /// </summary>
    public virtual async Task<GetSimulationDetailsQueryResult> HandleAsync(GetSimulationDetailsQuery query)
    {
        try
        {
            var simulation = await _simulationRepository.GetByKeyAsync(query.SimulationKey);
            if (simulation == null)
            {
                return new GetSimulationDetailsQueryResult
                {
                    Success = false,
                    Message = "Simulation not found"
                };
            }

            // Deserialize steps from JSON
            SimulationStep[]? steps = null;
            if (!string.IsNullOrWhiteSpace(simulation.SimulationStepsJson))
            {
                try
                {
                    steps = JsonSerializer.Deserialize<SimulationStep[]>(simulation.SimulationStepsJson);
                }
                catch (JsonException)
                {
                    // If deserialization fails, return empty array
                    steps = Array.Empty<SimulationStep>();
                }
            }

            return new GetSimulationDetailsQueryResult
            {
                Success = true,
                Simulation = simulation,
                Steps = steps ?? Array.Empty<SimulationStep>()
            };
        }
        catch (Exception ex)
        {
            return new GetSimulationDetailsQueryResult
            {
                Success = false,
                Message = $"Error retrieving simulation details: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Handle GetSimulationUsersQuery - get users for autocomplete/selection
    /// </summary>
    public virtual async Task<GetSimulationUsersQueryResult> HandleAsync(GetSimulationUsersQuery query)
    {
        try
        {
            var users = await _userRepository.GetActiveUsersAsync();

            // Filter by search text if provided
            if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                var searchLower = query.SearchText.ToLower();
                users = users.Where(u =>
                    u.FirstName.ToLower().Contains(searchLower) ||
                    u.LastName.ToLower().Contains(searchLower) ||
                    u.PhoneNumber.Contains(query.SearchText) ||
                    u.Email.ToLower().Contains(searchLower) ||
                    u.KeyField.ToLower().Contains(searchLower));
            }

            var userInfos = users.Select(u => new SimulationUserDto
            {
                UserKey = u.Key,
                FirstName = u.FirstName,
                LastName = u.LastName,
                PhoneNumber = u.PhoneNumber,
                Email = u.Email,
                UserId = u.KeyField
            }).ToList();

            return new GetSimulationUsersQueryResult
            {
                Success = true,
                Users = userInfos
            };
        }
        catch (Exception ex)
        {
            return new GetSimulationUsersQueryResult
            {
                Success = false,
                Message = $"Error retrieving users: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Handle GetSimulationDevicesQuery - search all devices or filter by user
    /// </summary>
    public virtual async Task<GetSimulationDevicesQueryResult> HandleAsync(GetSimulationDevicesQuery query)
    {
        try
        {
            IEnumerable<Common.Entities.UserDevice> devices;

            if (query.UserKey != null)
            {
                // Get devices for specific user
                devices = await _userDeviceRepository.GetByUserKeyAsync(query.UserKey);
            }
            else
            {
                // Get all devices
                devices = await _userDeviceRepository.GetAllAsync();
            }

            // Filter by search text if provided
            if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                var searchLower = query.SearchText.ToLower();
                devices = devices.Where(d =>
                    d.DeviceUid.ToLower().Contains(searchLower) ||
                    d.MAC.ToLower().Contains(searchLower) ||
                    d.DeviceType.ToString().ToLower().Contains(searchLower) ||
                    d.OperatingSystem.ToString().ToLower().Contains(searchLower));
            }

            // Fetch user info for each device
            var deviceInfos = new List<SimulationDeviceDto>();
            foreach (var device in devices)
            {
                var user = await _userRepository.GetByKeyAsync(device.UserKey);
                deviceInfos.Add(new SimulationDeviceDto
                {
                    DeviceKey = device.Key,
                    DeviceUid = device.DeviceUid,
                    DeviceType = device.DeviceType.ToString(),
                    OperatingSystem = device.OperatingSystem.ToString(),
                    MAC = device.MAC,
                    UserKeyField = device.UserKey.KeyField,
                    UserFullName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : null
                });
            }

            return new GetSimulationDevicesQueryResult
            {
                Success = true,
                Devices = deviceInfos
            };
        }
        catch (Exception ex)
        {
            return new GetSimulationDevicesQueryResult
            {
                Success = false,
                Message = $"Error retrieving devices: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Handle GetSimulationUserDevicesQuery - get devices for a specific user
    /// </summary>
    public virtual async Task<GetSimulationUserDevicesQueryResult> HandleAsync(GetSimulationUserDevicesQuery query)
    {
        try
        {
            var devices = await _userDeviceRepository.GetByUserKeyAsync(query.UserKey);

            var deviceInfos = devices.Select(d => new SimulationDeviceDto
            {
                DeviceKey = d.Key,
                DeviceUid = d.DeviceUid,
                DeviceType = d.DeviceType.ToString(),
                OperatingSystem = d.OperatingSystem.ToString(),
                MAC = d.MAC
            }).ToList();

            return new GetSimulationUserDevicesQueryResult
            {
                Success = true,
                Devices = deviceInfos
            };
        }
        catch (Exception ex)
        {
            return new GetSimulationUserDevicesQueryResult
            {
                Success = false,
                Message = $"Error retrieving devices: {ex.Message}"
            };
        }
    }
}
