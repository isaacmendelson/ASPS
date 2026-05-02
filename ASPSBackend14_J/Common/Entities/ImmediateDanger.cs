using Common.Enums;
using Common.Models;
using Newtonsoft.Json;
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

        public ImmediateDanger() { }

        public ImmediateDanger(DateTime timestamp, string deviceUid, string userKeyField, string? deviceKeyField, string? deviceAlertKeyField,
            UserDevice? device, DeviceAlert? deviceAlert, DateTime? endTime, User user, Key? scamInProgressKey = null, ProtectiveAction[]? protectiveActions = null)
        {
            Timestamp = timestamp;
            DeviceUid = deviceUid;
            UserKeyField = userKeyField;
            DeviceKeyField = deviceKeyField;
            DeviceAlertKeyField = deviceAlertKeyField;
            this.ProtectiveActionsJson = JsonConvert.SerializeObject(protectiveActions ?? []);
            ScamInProgressKeyField = scamInProgressKey?.Value;
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
        public string? ProtectiveActionsJson { get; set; }
        

        [NotMapped]
        public ProtectiveAction[] ProtectiveActions { 
            get
            {
                return JsonConvert.DeserializeObject<ProtectiveAction[]>(ProtectiveActionsJson ?? "[]") ?? Array.Empty<ProtectiveAction>();
            }
        }

        [NotMapped]
        [ForeignKey(nameof(DeviceKeyField))]
        public UserDevice? Device { get; set; }

        [NotMapped]
        [ForeignKey(nameof(DeviceAlertKeyField))]
        public DeviceAlert? DeviceAlert { get; set; }
        
        public DateTime? EndTime { get; set; }

        [ForeignKey(nameof(UserKeyField))]
        public User User { get; set; }

        

        public string? ScamInProgressKeyField { get; set; }

        // In-memory navigation only — Key is a value object, persisted via ScamInProgressKeyField above
        [NotMapped]
        public Key? ScamInProgressKey { get; set; }
        public bool IsClosed()
        {

            return this.EndTime is null;
        }
    }
}
