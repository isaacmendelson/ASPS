using Common.Entities;
using Common.Enums;
using Common.Models;
using Common.Models.Alerts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Views
{
    public class RemoteAccessAlertView : DeviceAlertView
    {
        public RemoteAccessAlertView(RemoteAccessAlertEntity entity)
          : base(entity)
        {
            this.RemoteAccessApp = entity.RemoteAccessApp;
            this.RunningProcesses = entity.RunningProcesses;
            this.ConnectionUrl = entity.ConnectionUrl;
            this.ConnectionStatus = entity.ConnectionStatus;
            this.ConnectionsCount = entity.ConnectionsCount;
            this.SessionStatus = (int)entity.SessionStatus;
            this.RemoteId = entity.RemoteId;
            this.RemoteName = entity.RemoteName;
            this.LoggedUser = entity.LoggedUser;
            this.ConnectionId = entity.ConnectionId;
            this.Software = entity.Software;
        }

        public RemoteAccessAlertView(
            Key key, string? alertId, string alertType, Priority priority,
            DateTime timestamp, string token, string deviceUid,
            DeviceType deviceType, OperatingSystemType operatingSystem, string? mAC, Key? userKey,
            RemoteAccessApp remoteAccessApp, int runningProcesses, string connectionUrl,
            ConnectionStatus connectionStatus, int connectionsCount, int sessionStatus, BrowserTab[]? browserTabs)
            : base(key, alertId, alertType, priority, timestamp, token, deviceUid, deviceType, operatingSystem, mAC ?? string.Empty, userKey)
        {
            RemoteAccessApp = remoteAccessApp;
            RunningProcesses = runningProcesses;
            ConnectionUrl = connectionUrl;
            ConnectionStatus = connectionStatus;
            ConnectionsCount = connectionsCount;
            SessionStatus = sessionStatus;
            BrowserTabs = browserTabs;
        }

        public RemoteAccessApp RemoteAccessApp { get; set; }
        public int RunningProcesses { get; set; }
        public string ConnectionUrl { get; set; } = string.Empty;
        public ConnectionStatus ConnectionStatus { get; set; }
        public int ConnectionsCount { get; set; }
        public int SessionStatus { get; set; }

        // Session identity / forensics
        public string? RemoteId { get; set; }
        public string? RemoteName { get; set; }
        public string? LoggedUser { get; set; }
        public string? ConnectionId { get; set; }
        public string? Software { get; set; }

        public BrowserTab[]? BrowserTabs { get; set; }

        [NotMapped]
        public string TypeName => "RemoteAccessAlert";
    }
}
