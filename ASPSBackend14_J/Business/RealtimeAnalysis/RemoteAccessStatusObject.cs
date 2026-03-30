using Business.DomainEvents;
using Business.RealtimeAnalysis.Indicators;
using Business.Views;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using Common.ViewModels;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis.UserDomain
{
    public class RemoteAccessStatusObject
    {
        public RemoteAccessStatusObject(DateTime timestamp, string deviceUid, RemoteAccessDirection remoteAccessDirection, RemoteAccessApp remoteAccessApp,
            bool isRemoteAccessAppActive, bool isRemoteAccessSessionActive)
        {
            Timestamp = timestamp;
            DeviceUid = deviceUid;
            RemoteAccessDirection = remoteAccessDirection;
            RemoteAccessApp = remoteAccessApp;
            IsRemoteAccessAppActive = isRemoteAccessAppActive;
            this.isRemoteAccessSessionActive = isRemoteAccessSessionActive;
        }

        public DateTime Timestamp { get; set; }
        public string DeviceUid { get; set; }
        public RemoteAccessDirection RemoteAccessDirection { get; set; }
        public RemoteAccessApp RemoteAccessApp { get; set; }
        public bool IsRemoteAccessAppActive { get; set; }
        public bool isRemoteAccessSessionActive { get; set; }

    }
}
