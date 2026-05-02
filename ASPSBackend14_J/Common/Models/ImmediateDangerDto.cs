using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    public abstract class ImmediateDangerDto : IImmediateDangerView
    {
        public ImmediateDangerDto(Key key, string deviceUid, string userKey, Key? deviceKey, Key? deviceAlertKey = null
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
        public ImmediateDangerDto(ImmediateDanger entity)
        {
            Timestamp = entity.Timestamp;
            DeviceUid = entity.DeviceUid;
            UserKey = entity.UserKeyField;
            DeviceKey = entity.Device?.Key;
            DeviceAlertKey = entity.DeviceAlert.AlertId is not null && entity.DeviceAlert is not null ? new Key(entity.DeviceAlert.AlertType, entity.DeviceAlert.AlertId) : null;
            ScamInProgressKey = entity.ScamInProgressKey;
            ProtectiveActions = entity.ProtectiveActions ?? [];
        }

        public Key Key { get; set; }
        public DateTime Timestamp { get; set; }
        public string DeviceUid { get; set; }
        public string UserKey { get; set; }
        public Key? DeviceKey { get; set; }
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
