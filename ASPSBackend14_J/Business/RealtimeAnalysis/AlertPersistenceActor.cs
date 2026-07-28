using Business.Data.EF.Repositories;
using Business.DomainEvents;
using Business.Messaging;
using Business.Views;
using Common.Entities;
using Common.Exceptions;
using Common.Interfaces;
using Common.Models.Alerts;
using Interface.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis
{
    public class AlertPersistenceActor : IDomainEventHandler
    {
        private readonly ILogger<AlertPersistenceActor> _logger;
        private readonly ASView _asView;
        private readonly IServiceProvider _serviceProvider;

        public AlertPersistenceActor(ILoggerFactory loggerFactory, ASView asView, IServiceProvider serviceProvider)
        {
            _logger = loggerFactory.CreateLogger<AlertPersistenceActor>();
            _asView = asView;
            _serviceProvider = serviceProvider;
        }

        public Type[] GetHandleableEvents()
        {
            return new[] { typeof(DeviceAlertReceived) };
        }

        public async Task Handle(IDomainEvent evt)
        {
            if (evt is DeviceAlertReceived analysisEvent)
            {
                await HandleDeviceAlertReceivedAsync(analysisEvent);
            }
        }
        public async Task HandleDeviceAlertReceivedAsync(DeviceAlertReceived alertReceived)
        {
            var vm = alertReceived.Alert;
            var userDevice = _asView.FindUserDeviceByDeviceUid(vm.DeviceInfo.DeviceUid);
            if (userDevice == null)
            {
                var errorMsg = $"Device not found: {vm.DeviceInfo.DeviceUid}";
                _logger.LogWarning(errorMsg);
                throw new DomainException(
                    new Common.Exceptions.ErrorMessage("DeviceNotFound", errorMsg, Common.Enums.ResultStatusCode.NotFound));
            }

            if(userDevice?.UserKey is not null && vm.DeviceInfo.UserKey is null)
            {
                vm.DeviceInfo.UserKey = userDevice?.UserKey;
            }
            DeviceAlertEntity? alertEntity = null;
            var entityKey = !string.IsNullOrEmpty(alertReceived.DeviceAlertEntityKey)
                ? alertReceived.DeviceAlertEntityKey
                : Guid.NewGuid().ToString();
            switch (vm)
            {
                case UrlAlert u:
                    alertEntity = new UrlAlertEntity()
                    {
                        DateCreated = DateTime.UtcNow,
                        KeyField = entityKey,
                        AlertType = vm.AlertType,
                        Priority = vm.Priority,
                        Timestamp = vm.Timestamp,
                        Token = vm.Token,
                        Status = vm.Status,
                        DeviceUid = vm.DeviceInfo.DeviceUid,
                        DeviceType = userDevice.DeviceType,
                        OperatingSystem = userDevice.OperatingSystem,
                        IPAddress = u.IPAddress ?? "",
                        Url = u.Url,
                        UserKeyField = userDevice.UserKeyField,
                        UserAgent = u.UserAgent,
                        TabId = u.TabId,
                        MAC = userDevice.MAC,
                        UserKey = userDevice?.UserKey ?? u.DeviceInfo.UserKey
                    };
                   
                    break;

                case RemoteAccessAlert r:
                    alertEntity = new RemoteAccessAlertEntity()
                    {
                        DateCreated = DateTime.UtcNow,
                        KeyField = entityKey,
                        AlertType = vm.AlertType,
                        Priority = vm.Priority,
                        Timestamp = vm.Timestamp,
                        Token = vm.Token,
                        ConnectionsCount = r.ConnectionsCount,
                        ConnectionStatus = r.ConnectionStatus,
                        ConnectionUrl = r.ConnectionUrl,
                        //DeviceInfo = r.DeviceInfo,
                        DeviceKey = r.DeviceInfo.Key,
                        DeviceKeyField = r.DeviceInfo.Key.Value,
                        RemoteAccessApp = r.RemoteAccessApp,
                        RunningProcesses = r.RunningProcesses,
                        SessionStatus = (Common.Enums.SessionStatus)r.SessionStatus,
                        RemoteOS = r.RemoteOS,
                        RemoteVersion = r.RemoteVersion,
                        ConnectionType = r.ConnectionType,
                        FileTransferActive = r.FileTransferActive,
                        FileTransfers = r.FileTransfers,
                        // Session identity / forensics (Phase G)
                        RemoteId = r.RemoteId,
                        RemoteName = r.RemoteName,
                        LoggedUser = r.LoggedUser,
                        ConnectionId = r.ConnectionId,
                        Software = r.Software,
                        // Wire fields (direction / confidence / geo)
                        Direction = r.Direction,
                        Confidence = r.Confidence,
                        RemoteCountry = r.RemoteCountry,
                        RemoteCountryCode = r.RemoteCountryCode,
                        Status = r.Status,
                        UserKey = userDevice.UserKey,
                        DeviceUid = vm.DeviceInfo.DeviceUid,
                        DeviceType = userDevice.DeviceType,
                        OperatingSystem = userDevice.OperatingSystem,
                        IPAddress = vm.DeviceInfo.IP ?? "",
                        UserKeyField = userDevice.UserKeyField,
                    };
                    break;

                case TrackUrlAlert t:
                    alertEntity = new TrackUrlAlertEntity()
                    {
                        DateCreated = DateTime.UtcNow,
                        KeyField = entityKey,
                        AlertType = vm.AlertType,
                        Priority = vm.Priority,
                        Timestamp = vm.Timestamp,
                        Token = vm.Token,
                        Status = vm.Status,
                        DeviceUid = vm.DeviceInfo.DeviceUid,
                        DeviceType = userDevice.DeviceType,
                        OperatingSystem = userDevice.OperatingSystem,
                        IPAddress = t.IPAddress,
                        Url = t.Url,
                        FromUrl = t.FromUrl,
                        Duration = t.Duration,
                        ScamInProgressKey = t.ScamInProgressKey,
                        UserAgent = t.UserAgent,
                        TabId = t.TabId,
                        Timezone = t.Timezone,
                        UserKeyField = userDevice.UserKeyField,
                    };
                    break;

                case TabClosedAlert tc:
                    alertEntity = new TabClosedAlertEntity()
                    {
                        DateCreated = DateTime.UtcNow,
                        KeyField = entityKey,
                        AlertType = vm.AlertType,
                        Priority = vm.Priority,
                        Timestamp = vm.Timestamp,
                        Token = vm.Token,
                        Status = vm.Status,
                        DeviceUid = vm.DeviceInfo.DeviceUid,
                        DeviceType = userDevice.DeviceType,
                        OperatingSystem = userDevice.OperatingSystem,
                        UserKeyField = userDevice.UserKeyField,
                        TabId = tc.TabId,
                        Url   = tc.Url,
                    };
                    break;

                case TabChangedAlert tch:
                    alertEntity = new TabChangedAlertEntity()
                    {
                        DateCreated = DateTime.UtcNow,
                        KeyField = entityKey,
                        AlertType = vm.AlertType,
                        Priority = vm.Priority,
                        Timestamp = vm.Timestamp,
                        Token = vm.Token,
                        Status = vm.Status,
                        DeviceUid = vm.DeviceInfo.DeviceUid,
                        DeviceType = userDevice.DeviceType,
                        OperatingSystem = userDevice.OperatingSystem,
                        UserKeyField = userDevice.UserKeyField,
                        TabId = tch.TabId,
                        Url = tch.Url,
                        IsSensitiveWebsite = tch.IsSensitiveWebsite,
                        IsLoggedIn = tch.IsLoggedIn
                    };
                    break;
            }

            // Snapshot of agent's ImmediateDanger flag at the time of the alert.
            // Applies to every alert type — set after the switch for centralisation.
            if (alertEntity != null)
            {
                alertEntity.ImmediateDanger = vm.DeviceInfo.ImmediateDanger;
                if (vm.MessagingIdentity is { } identity)
                {
                    alertEntity.SchemaVersion = Common.Generated.Messaging.V1.MessagingContractV1.SchemaVersion;
                    alertEntity.MessageId = identity.MessageId;
                    alertEntity.CorrelationId = identity.CorrelationId;
                    alertEntity.RequestId = identity.RequestId;
                    alertEntity.CanonicalUrl = identity.Url;
                }
            

                // Save alert entity to database using scoped repository
                _logger.LogInformation( 
                    $"[AlertPersistenceActor] Saved device alert: " +
                    $"Key={alertEntity.Key.Value}, " +
                    $"AlertType={vm.AlertType}, " +
                    $"Priority={vm.Priority}, " +
                    $"DeviceUid={vm.DeviceInfo.DeviceUid}");

                using (var scope = _serviceProvider.CreateScope())
                {
                    var alertRepository = scope.ServiceProvider.GetRequiredService<IDeviceAlertRepository>();
                    await alertRepository.AddAsync(alertEntity);
                    _logger.LogInformation($"Device alert saved to database: {alertEntity.Key}");

                }
            }

        }
    }
}
