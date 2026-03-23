using Common.Entities;
using Common.Enums;
using Common.Models;
using Common.Models.Alerts;
using Interface.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Business.Services;

/// <summary>
/// Background service that executes simulation steps with delays
/// </summary>
public class SimulationRunner : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<SimulationRunner> _logger;
    private readonly ConcurrentQueue<SimulationRun> _runQueue = new();
    private readonly SemaphoreSlim _queueSemaphore = new(0);

    public SimulationRunner(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<SimulationRunner> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Queue a simulation for execution
    /// </summary>
    public void QueueSimulation(Key simulationKey, Key requestorKey)
    {
        var run = new SimulationRun
        {
            SimulationKey = simulationKey,
            RequestorKey = requestorKey,
            QueuedAt = DateTime.UtcNow
        };

        _runQueue.Enqueue(run);
        _queueSemaphore.Release();
        
        _logger.LogInformation($"Simulation {simulationKey.Value} queued for execution");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SimulationRunner background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for a simulation to be queued
                await _queueSemaphore.WaitAsync(stoppingToken);

                if (_runQueue.TryDequeue(out var run))
                {
                    _logger.LogInformation($"Starting execution of simulation {run.SimulationKey.Value}");
                    await ExecuteSimulationAsync(run, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Service is stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SimulationRunner loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("SimulationRunner background service stopped");
    }

    private async Task ExecuteSimulationAsync(SimulationRun run, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var simulationRepo = scope.ServiceProvider.GetRequiredService<ISimulationRepository>();
        var deviceAlertRepo = scope.ServiceProvider.GetRequiredService<IDeviceAlertRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var deviceRepo = scope.ServiceProvider.GetRequiredService<IUserDeviceRepository>();

        try
        {
            // Load simulation
            var simulation = await simulationRepo.GetByKeyAsync(run.SimulationKey);
            if (simulation == null)
            {
                _logger.LogWarning($"Simulation {run.SimulationKey.Value} not found");
                return;
            }

            // Deserialize steps
            if (string.IsNullOrWhiteSpace(simulation.SimulationStepsJson))
            {
                _logger.LogWarning($"Simulation {run.SimulationKey.Value} has no steps");
                return;
            }

            var steps = JsonSerializer.Deserialize<SimulationStep[]>(simulation.SimulationStepsJson);
            if (steps == null || steps.Length == 0)
            {
                _logger.LogWarning($"Simulation {run.SimulationKey.Value} has empty steps");
                return;
            }

            _logger.LogInformation($"Executing {steps.Length} steps for simulation {simulation.Name}");

            // Execute each step
            foreach (var step in steps.OrderBy(s => s.Sequence))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation($"Simulation {simulation.Name} cancelled");
                    break;
                }

                // Wait for the delay
                if (step.DelayMs > 0)
                {
                    _logger.LogDebug($"Waiting {step.DelayMs}ms before step {step.Sequence}");
                    await Task.Delay((int)step.DelayMs, cancellationToken);
                }

                // Execute the step
                await ExecuteStepAsync(step, simulation, deviceAlertRepo, userRepo, deviceRepo, cancellationToken);
            }

            _logger.LogInformation($"Simulation {simulation.Name} completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error executing simulation {run.SimulationKey.Value}");
        }
    }

    private async Task ExecuteStepAsync(
        SimulationStep step,
        Simulation simulation,
        IDeviceAlertRepository deviceAlertRepo,
        IUserRepository userRepo,
        IUserDeviceRepository deviceRepo,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation($"Executing step {step.Sequence}: {step.AlertType} for user {step.UserId}");

            // Validate user exists
            var userKey = new Key("User", step.UserId);
            var user = await userRepo.GetByKeyAsync(userKey);
            if (user == null)
            {
                _logger.LogWarning($"User {step.UserId} not found, skipping step {step.Sequence}");
                return;
            }

            // Validate device exists
            var device = await deviceRepo.GetByDeviceUidAsync(step.DeviceUid);
            
            // Deserialize alert from JSON
            DeviceAlert? alert = step.AlertType switch
            {
                "UrlAlert" => JsonSerializer.Deserialize<UrlAlert>(step.AlertJson),
                "RemoteAccessAlert" => JsonSerializer.Deserialize<RemoteAccessAlert>(step.AlertJson),
                "TrackUrlAlert" => JsonSerializer.Deserialize<TrackUrlAlert>(step.AlertJson),
                _ => null
            };

            if (alert == null)
            {
                _logger.LogWarning($"Could not deserialize alert for step {step.Sequence}, type: {step.AlertType}");
                return;
            }

            // Create device alert entity
            DeviceAlertEntity alertEntity = step.AlertType switch
            {
                "UrlAlert" when alert is UrlAlert urlAlert => new UrlAlertEntity
                {
                    KeyField = Guid.NewGuid().ToString(),
                    AlertType = "UrlAlert",
                    Priority = step.Priority,
                    Timestamp = DateTime.UtcNow,
                    Token = Guid.NewGuid().ToString(),
                    DeviceUid = step.DeviceUid,
                    DeviceType = device?.DeviceType ?? DeviceType.Unknown,
                    OperatingSystem = device?.OperatingSystem ?? OperatingSystemType.Unknown,
                    MAC = device?.MAC ?? string.Empty,
                    UserKeyField = step.UserId,
                    DeviceKeyField = device?.KeyField,
                    Status = AlertFlagStatus.Unknown,
                    Url = urlAlert.Url,
                    UserAgent = urlAlert.UserAgent,
                    TrackerKeys = JsonSerializer.Serialize(urlAlert.Trackers),
                    IFrameDomains = JsonSerializer.Serialize(urlAlert.IFrameDomains)
                },
                "RemoteAccessAlert" when alert is RemoteAccessAlert raAlert => new RemoteAccessAlertEntity
                {
                    KeyField = Guid.NewGuid().ToString(),
                    AlertType = "RemoteAccessAlert",
                    Priority = step.Priority,
                    Timestamp = DateTime.UtcNow,
                    Token = Guid.NewGuid().ToString(),
                    DeviceUid = step.DeviceUid,
                    DeviceType = device?.DeviceType ?? DeviceType.Unknown,
                    OperatingSystem = device?.OperatingSystem ?? OperatingSystemType.Unknown,
                    MAC = device?.MAC ?? string.Empty,
                    UserKeyField = step.UserId,
                    DeviceKeyField = device?.KeyField,
                    Status = AlertFlagStatus.Unknown,
                    ConnectionUrl = raAlert.ConnectionUrl,
                    RemoteOS = raAlert.RemoteOS,
                    RemoteVersion = raAlert.RemoteVersion,
                    ConnectionType = raAlert.ConnectionType
                },
                "TrackUrlAlert" when alert is TrackUrlAlert tuAlert => new TrackUrlAlertEntity
                {
                    KeyField = Guid.NewGuid().ToString(),
                    AlertType = "TrackUrlAlert",
                    Priority = step.Priority,
                    Timestamp = DateTime.UtcNow,
                    Token = Guid.NewGuid().ToString(),
                    DeviceUid = step.DeviceUid,
                    DeviceType = device?.DeviceType ?? DeviceType.Unknown,
                    OperatingSystem = device?.OperatingSystem ?? OperatingSystemType.Unknown,
                    MAC = device?.MAC ?? string.Empty,
                    UserKeyField = step.UserId,
                    DeviceKeyField = device?.KeyField,
                    Status = AlertFlagStatus.Unknown,
                    Url = tuAlert.Url,
                    FromUrl = tuAlert.FromUrl,
                    Duration = tuAlert.Duration,
                    UserAgent = tuAlert.UserAgent
                },
                _ => throw new InvalidOperationException($"Unknown alert type: {step.AlertType}")
            };

            // Save alert to database
            await deviceAlertRepo.AddAsync(alertEntity);

            _logger.LogInformation($"Step {step.Sequence} executed: Created {step.AlertType} alert {alertEntity.KeyField}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error executing step {step.Sequence}");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SimulationRunner is stopping");
        return base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Internal class to track simulation runs
/// </summary>
internal class SimulationRun
{
    public Key SimulationKey { get; set; } = new Key();
    public Key RequestorKey { get; set; } = new Key();
    public DateTime QueuedAt { get; set; }
}
