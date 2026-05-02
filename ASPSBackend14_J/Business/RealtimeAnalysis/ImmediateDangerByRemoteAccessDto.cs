using Common.Entities;
using Common.Enums;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis
{
    internal class ImmediateDangerByRemoteAccessDto : ImmediateDangerDto
    {
        public ImmediateDangerByRemoteAccessDto(RemoteAccessApp? remoteAccessApp, string? sensitiveUrl, string deviceUid, string userKey, string? deviceKey, Key? deviceAlertKey = null
            , Key? scamInProgressKey, ProtectiveAction[] protectiveActions
            ) : base(deviceUid, userKey, deviceKey, deviceAlertKey)
        {
            this.RemoteAccessApp = remoteAccessApp;
                this.SensitiveUrl = sensitiveUrl;
        }

        public RemoteAccessApp? RemoteAccessApp { get; set; }
        public string? SensitiveUrl { get; set; }

    }
}
