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
    public class UserDeviceView : ASItemView
    {
        public UserDeviceView(UserDevice entity) 
            : base(entity.Key, "u d ")
        {
            this.DeviceUid = entity.DeviceUid;
            this.DeviceType = entity.DeviceType;
            this.OperatingSystem = (OperatingSystemType)entity.OperatingSystem;
            this.MAC = entity.MAC;
            this.IMEI = entity.IMEI;
            this.Serial = entity.Serial;
            this.MonitoringStatus = entity.MonitoringStatus;
            this.BiosSerial = entity.BiosSerial;
            this.Make = entity.Make;
            this.Model = entity.Model;
            this.MonitoringStatus = entity.MonitoringStatus;
            this.PhoneNumber = entity.PhoneNumber;

        }   

        public string DeviceUid { get; set; } = string.Empty;
        public DeviceType DeviceType { get; set; }
        public OperatingSystemType OperatingSystem { get; set; }
        public string MAC { get; set; } = string.Empty;
        public string? IMEI { get; private set; }
        public string? Serial { get; }
        public DeviceMonitoringStatus MonitoringStatus { get; }
        public string? PhoneNumber { get; }
        public string? BiosSerial { get; }
        public string? Make { get; }
        public string? Model { get; }
    }

}
