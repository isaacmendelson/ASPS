using Common.Enums;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis
{
    public abstract class ImmediateDangerDto
    {
        public ImmediateDangerDto(string deviceUid, string userKey, string? deviceKey, Key? deviceAlertKey = null
            , Key? scamInProgressKey = null, ProtectiveAction[]? protectiveActions = null)
        {
            Timestamp = DateTime.UtcNow;
            DeviceUid = deviceUid;
            UserKey = userKey;
            DeviceKey = deviceKey;
            DeviceAlertKey = deviceAlertKey;
            ScamInProgressKey = scamInProgressKey;
            ProtectiveActions = protectiveActions ?? [];

        }

        public DateTime Timestamp { get; set; }
        public string DeviceUid { get; set; }
        public string UserKey { get; set; }
        public string? DeviceKey { get; set; }
        public Key? DeviceAlertKey { get; set; }
        public Key? ScamInProgressKey { get; set; }
        public ProtectiveAction[] ProtectiveActions { get; set; } = Array.Empty<ProtectiveAction>();

        public DateTime? EndTime { get; set; }


        public bool IsClosed()
        {

            return this.EndTime is null;
        }

    }
}
