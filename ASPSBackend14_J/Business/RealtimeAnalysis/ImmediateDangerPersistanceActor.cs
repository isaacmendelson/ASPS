using Business.DomainEvents;
using Business.RealtimeAnalysis.UserDomain;
using Common.Entities;
using Common.Interfaces;
using Common.Models;
using Interface.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Business.RealtimeAnalysis;

/// <summary>
/// Listens for ImmediateDangerDetected events and persists them to the database,
/// then publishes ImmediateDangerAdded so downstream caches (ASView, per-user UDAnalysis)
/// can react. Single-responsibility: only handles persistence.
/// </summary>
public class ImmediateDangerPersistanceActor : IDomainEventHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ImmediateDangerPersistanceActor> _logger;

    // Cached singleton handlers (built lazily — DI cannot inject IEnumerable<IDomainEventHandler>
    // here directly because this actor is itself an IDomainEventHandler, which would form a cycle).
    private List<IDomainEventHandler>? _cachedSingletonHandlers;
    private readonly object _cacheLock = new();

    public ImmediateDangerPersistanceActor(
        IServiceProvider serviceProvider,
        ILogger<ImmediateDangerPersistanceActor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Type[] GetHandleableEvents() => new[] { typeof(ImmediateDangerDetected), typeof(ImmediateDangerEnded) };

    public async Task Handle(IDomainEvent evt)
    {
       
        switch(evt)
        {
            case ImmediateDangerDetected a:
                await HandleImmediateDangerDetectedAsync(a);
                break;
            case ImmediateDangerEnded e:
                await HandleImmediateDangerEndedAsync(e);
                break;
        }
    }

    private async Task HandleImmediateDangerEndedAsync(ImmediateDangerEnded evt)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IImmediateDangerRepository>();


        var rec = await repository.GetByKeyAsync(evt.ImmediateDangerKey);
        if (rec != null)
        {
            rec.EndTime = DateTime.UtcNow;
            await repository.UpdateAsync(rec);
            _logger.LogInformation(
                "[ImmediateDangerPersistanceActor] Updated ImmediateDanger with end time: Key={Key}, User={UserKey}, Device={DeviceUid}",
                rec.KeyField, rec.UserKeyField, rec.DeviceUid);
        }

        // Notify downstream handlers (per-user UDAnalysisManager + UDAnalysis,
        // singleton handlers) that an ImmediateDanger was closed — they need
        // to drop it from in-memory state.
        PublishImmediateDangerEnded(evt);
    }
    private async Task HandleImmediateDangerDetectedAsync(ImmediateDangerDetected evt)
    {
        try
        {
            var dto = evt.ImmediateDanger;
            if (dto == null)
            {
                _logger.LogWarning("ImmediateDangerDetected fired without ImmediateDanger payload — skipping persistence");
                return;
            }

            var entity = MapDtoToEntity(dto);
            if (entity == null)
            {
                _logger.LogWarning("Unsupported ImmediateDangerDto subtype: {Type} — skipping persistence", dto.GetType().Name);
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IImmediateDangerRepository>();
            await repository.AddAsync(entity);

            _logger.LogInformation(
                "[ImmediateDangerPersistanceActor] Saved immediate danger: Key={Key}, User={UserKey}, Device={DeviceUid}, Type={Type}",
                entity.KeyField, entity.UserKeyField, entity.DeviceUid, entity.GetType().Name);

            // Notify downstream handlers (ASView, the user's UDAnalysis, etc) that a new ImmediateDanger was persisted
            PublishImmediateDangerAdded(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error persisting ImmediateDangerDetected event");
        }
    }

    private void PublishImmediateDangerAdded(ImmediateDangerDto dto)
    {
        var handlers = BuildPerUserHandlers(dto.UserKey, eventName: "ImmediateDangerAdded");

        // Per-event publisher because the per-user manager+analysis varies between events
        var publisher = new DomainEventPublisher(handlers);
        var evtAdded = new ImmediateDangerAdded(dto);
        publisher.Register(evtAdded);
        publisher.RaiseAll();
    }

    private void PublishImmediateDangerEnded(ImmediateDangerEnded evt)
    {
        var userKeyValue = evt.UserKey?.Value ?? string.Empty;
        var handlers = BuildPerUserHandlers(userKeyValue, eventName: "ImmediateDangerEnded");

        var publisher = new DomainEventPublisher(handlers);
        publisher.Register(evt);
        publisher.RaiseAll();
    }

    /// <summary>
    /// Build a handler list for per-user immediate-danger event publishing:
    /// cached singleton handlers (excluding self) + the user's UDAnalysisManager
    /// + the user's UDAnalysis. UDAnalysisManager and UDAnalysis are not in DI —
    /// they live inside UserDomainManagerService._userManagers.
    /// </summary>
    private List<IDomainEventHandler> BuildPerUserHandlers(string userKeyValue, string eventName)
    {
        var handlers = new List<IDomainEventHandler>(GetCachedSingletonHandlers());

        try
        {
            var managerService = _serviceProvider.GetService<UserDomainManagerService>();
            if (managerService != null && !string.IsNullOrEmpty(userKeyValue))
            {
                var userKey = new Key("User", userKeyValue);
                var manager = managerService.GetOrCreateManagerForUser(userKey);
                if (manager != null)
                {
                    handlers.Add(manager);
                    if (manager.Analysis != null)
                    {
                        handlers.Add(manager.Analysis);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to attach per-user handlers for user {UserKey} on {Event} — event will still flow to singleton handlers",
                userKeyValue, eventName);
        }

        return handlers;
    }

    private List<IDomainEventHandler> GetCachedSingletonHandlers()
    {
        if (_cachedSingletonHandlers != null) return _cachedSingletonHandlers;
        lock (_cacheLock)
        {
            if (_cachedSingletonHandlers != null) return _cachedSingletonHandlers;

            // Resolve lazily — by now all singletons are constructed, so no construction cycle.
            // Exclude self to avoid re-entry on our own publishes.
            _cachedSingletonHandlers = _serviceProvider.GetServices<IDomainEventHandler>()
                .Where(h => !ReferenceEquals(h, this))
                .ToList();
            _logger.LogInformation(
                "ImmediateDangerPersistanceActor cached {Count} singleton handlers",
                _cachedSingletonHandlers.Count);
            return _cachedSingletonHandlers;
        }
    }

    private static ImmediateDanger? MapDtoToEntity(ImmediateDangerDto dto)
    {
        var keyField = Guid.NewGuid().ToString();
        var protectiveActionsJson = JsonConvert.SerializeObject(dto.ProtectiveActions ?? Array.Empty<ProtectiveAction>());

        return dto switch
        {
            ImmediateDangerByRemoteAccessDto rd => new ImmediateDangerByRemoteAccess
            {
                KeyField = dto.Key.Value,
                Timestamp = dto.Timestamp,
                DeviceUid = dto.DeviceUid,
                UserKeyField = dto.UserKey,
                DeviceKeyField = dto.DeviceKey?.Value,
                DeviceAlertKeyField = dto.DeviceAlertKey?.Value,
                ScamInProgressKeyField = dto.ScamInProgressKey?.Value,
                ProtectiveActionsJson = protectiveActionsJson,
                EndTime = dto.EndTime,
                RemoteAccessApp = rd.RemoteAccessApp,
                SensitiveUrl = rd.SensitiveUrl,
                User = null!
            },
            _ => null
        };
    }
}
