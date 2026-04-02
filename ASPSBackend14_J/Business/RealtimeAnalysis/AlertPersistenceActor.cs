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
        private readonly IDeviceAlertRepository _deviceAlertRepository;
        private readonly ILogger<AlertPersistenceActor> _logger;
        private readonly ASView _asView;
        private readonly IServiceProvider _serviceProvider;

        public AlertPersistenceActor(IDeviceAlertRepository deviceAlertRepository, ILoggerFactory _loggerFactory, ASView asView, IServiceProvider serviceProvider)
        {
            _deviceAlertRepository = deviceAlertRepository;
            this._logger = _loggerFactory.CreateLogger<AlertPersistenceActor>();
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
            }

            // Save alert entity to database using scoped repository
            if (alertEntity != null)
            {
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
