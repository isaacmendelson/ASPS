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
    public  class DeviceAlertView: ASItemView , IDeviceAlertView
    {
        public DeviceAlertView()
        {
            Key = new Key();
            AlertId = null;
            AlertType = string.Empty;
            Priority = Common.Enums.Priority.Low;
            Timestamp = DateTime.UtcNow;
            Token = string.Empty;
            DeviceUid = string.Empty;
            DeviceType = Common.Enums.DeviceType.Unknown;
            OperatingSystem = OperatingSystemType.Unknown;
            MAC = string.Empty;
            UserKey = null;
        }

        public DeviceAlertView(DeviceAlertEntity entity)
              : base(entity.Key, entity.Tag.Name)
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

        public DeviceAlertView(Key key, 
            //string name, 
            string? alertId, string alertType, Priority priority, 
            DateTime timestamp, string token, string deviceUid, 
            DeviceType deviceType, OperatingSystemType operatingSystem, string? mAC, Key? userKey)
            : base(key, alertType)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            AlertId = alertId;
            AlertType = alertType ?? throw new ArgumentNullException(nameof(alertType));
            Priority = priority;
            Timestamp = timestamp;
            Token = token ?? throw new ArgumentNullException(nameof(token));
            DeviceUid = deviceUid ?? throw new ArgumentNullException(nameof(deviceUid));
            DeviceType = deviceType;
            OperatingSystem = operatingSystem;
            MAC = mAC ?? string.Empty;
            UserKey = userKey;
        }

        //public DeviceAlert? Alert { get; private set; }
        public Key Key { get; private set; }
        public string? AlertId { get;  set; }
        public string AlertType { get;  set; }
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
