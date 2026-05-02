using Business.DomainEvents;
using Common.Entities;
using Common.Interfaces;
using Common.Models;
using Interface.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Business.RealtimeAnalysis;

/// <summary>
/// Listens for ImmediateDangerDetected events and persists them to the database.
/// Single-responsibility: only handles persistence — no analysis, no notifications.
/// </summary>
public class ImmediateDangerPersistanceActor : IDomainEventHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ImmediateDangerPersistanceActor> _logger;

    public ImmediateDangerPersistanceActor(
        IServiceProvider serviceProvider,
        ILogger<ImmediateDangerPersistanceActor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Type[] GetHandleableEvents() => new[] { typeof(ImmediateDangerDetected) };

    public async Task Handle(IDomainEvent evt)
    {
        if (evt is ImmediateDangerDetected dangerEvent)
        {
            await HandleImmediateDangerDetectedAsync(dangerEvent);
        }
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error persisting ImmediateDangerDetected event");
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
                KeyField = keyField,
                Timestamp = dto.Timestamp,
                DeviceUid = dto.DeviceUid,
                UserKeyField = dto.UserKey,
                DeviceKeyField = dto.DeviceKey,
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
