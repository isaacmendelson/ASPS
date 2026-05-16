using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    [Serializable]
    [DataContract]
    public abstract class ImmediateDangerDto : IImmediateDangerView
    {
        public ImmediateDangerDto(Key key, string deviceUid, string userKey, Key? deviceKey, Key? deviceAlertKey = null
            , Key? scamInProgressKey = null, ProtectiveAction[]? protectiveActions = null)
        {
            Key = key;
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
            Key = entity.Key; 
            Timestamp = entity.Timestamp;
            DeviceUid = entity.DeviceUid;
            UserKey = entity.UserKeyField;
            DeviceKey = entity.Device?.Key;
            DeviceAlertKey = entity.DeviceAlert?.AlertId is not null && entity.DeviceAlert is not null ? new Key(entity.DeviceAlert.AlertType, entity.DeviceAlert.AlertId) : null;
            ScamInProgressKey = entity.ScamInProgressKey;
            ProtectiveActions = entity.ProtectiveActions ?? [];
        }

        [DataMember]
        public Key Key { get; set; }
        [DataMember] 
        public DateTime Timestamp { get; set; }
        [DataMember] 
        public string DeviceUid { get; set; }
        [DataMember] 
        public string UserKey { get; set; }

        [DataMember]
        public Key? DeviceKey { get; set; }
        [DataMember] 
        public Key? DeviceAlertKey { get; set; }
        [DataMember] 
        public Key? ScamInProgressKey { get; set; }
        [DataMember] 
        public ProtectiveAction[] ProtectiveActions { get; set; } = Array.Empty<ProtectiveAction>();
        
        [DataMember]
        public DateTime? EndTime { get; set; }


        public bool IsClosed()
        {

            return this.EndTime is not null && this.EndTime >= this.Timestamp;
        }

    }
}
