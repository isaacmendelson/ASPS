using Common.Enums;
using Common.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Entities
{
    public abstract class ImmediateDanger : Entity
    {

        protected ImmediateDanger() { }

        public ImmediateDanger(DateTime timestamp, string deviceUid, string userKeyField, string? deviceKeyField, string? deviceAlertKeyField, 
            ProtectiveAction[] protectiveActions, UserDevice? device, DeviceAlert? deviceAlert, DateTime? endTime, User user)
        {
            Timestamp = timestamp;
            DeviceUid = deviceUid;
            UserKeyField = userKeyField;
            DeviceKeyField = deviceKeyField;
            DeviceAlertKeyField = deviceAlertKeyField;
            ProtectiveActions = protectiveActions;
            Device = device;
            DeviceAlert = deviceAlert;
            EndTime = endTime;
            User = user;
        }

        public string Typename 
        { 
            get
            {
                return this.GetType().Name;
            }
         }
        public DateTime Timestamp { get; set; }
        
        public string DeviceUid { get; set; }
        public string UserKeyField { get; set; }
        public string? DeviceKeyField { get; set; }
        public string? DeviceAlertKeyField { get; set; }

        public ProtectiveAction[] ProtectiveActions { get; set; } = Array.Empty<ProtectiveAction>();

        [NotMapped]
        [ForeignKey(nameof(DeviceKeyField))]
        public UserDevice? Device { get; set; }

        [NotMapped]
        [ForeignKey(nameof(DeviceAlertKeyField))]
        public DeviceAlert? DeviceAlert { get; set; }
        
        public DateTime? EndTime { get; set; }

        [ForeignKey(nameof(UserKeyField))]
        public User User { get; set; }

        public bool IsClosed()
        {

            return this.EndTime is null;
        }
    }
}
