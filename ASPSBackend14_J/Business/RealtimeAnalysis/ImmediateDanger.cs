using Common.Enums;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis
{
    public class ImmediateDanger
    {
        public ImmediateDanger(RemoteAccessApp? remoteAccessApp, string? sensitiveUrl, string deviceUid, string? userKey, string? deviceKey, Key? scamInProgressKey, ProtectiveAction[] protectiveActions)
        {
            Timestamp = DateTime.UtcNow;
            RemoteAccessApp = remoteAccessApp;
            SensitiveUrl = sensitiveUrl;
            DeviceUid = deviceUid;
            UserKey = userKey;
            DeviceKey = deviceKey;
            ScamInProgressKey = scamInProgressKey;
            ProtectiveActions = protectiveActions;

        }

        public DateTime Timestamp { get; set; }
        public RemoteAccessApp? RemoteAccessApp { get; set; }
        public string?  SensitiveUrl { get; set; }
        public string DeviceUid { get; set; }
        public string? UserKey { get; set; }
        public string? DeviceKey { get; set; }
        public Key? ScamInProgressKey { get; set; }

        public ProtectiveAction[] ProtectiveActions { get; set; } = Array.Empty<ProtectiveAction>();

        public DateTime? EndTime { get; set; }


        public bool IsClosed()
        {
            return this.EndTime is null;
        }

    }
}
