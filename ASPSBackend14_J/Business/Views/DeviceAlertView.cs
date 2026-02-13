using Common.Entities;
using Common.Enums;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Views
{
    public class DeviceAlertView: ASItemView
    {
        public DeviceAlertView(DeviceAlertEntity entity)
              : base(entity.Key, "d a ")
        {
            this.Key = entity.Key;
            AlertId = entity.AlertId;
            AlertType = entity.AlertType;
            Priority = entity.Priority;
            Timestamp = entity.Timestamp;
            Token = entity.Token;
            DeviceUid = entity.DeviceUid;
            DeviceType = entity.DeviceType;
            OperatingSystem = entity.OperatingSystem;
            MAC = entity.MAC;
            UserKey = entity.UserKey;
        }

        //public DeviceAlert? Alert { get; private set; }
        public Key Key { get; private set; }
        public string? AlertId { get; private set; }
        public string AlertType { get; private set; }
        public Common.Enums.Priority Priority { get; private set; }
        public DateTime Timestamp { get; private set; }
        public string Token { get; private set; }
        public string DeviceUid { get; private set; }
        public Common.Enums.DeviceType DeviceType { get; private set; }
        public OperatingSystemType OperatingSystem { get; private set; }
        public string MAC { get; private set; }
        public Key? UserKey { get; private set; }
    }
}
