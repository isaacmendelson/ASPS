using Common.Entities;
using Common.Interfaces;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.DomainEvents
{
    public class ImmediateDangerEvent : DomainEvent
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        protected ImmediateDangerEvent() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        public ImmediateDangerEvent(Key userKeyField, string deviceUid, ImmediateDangerDto immediateDanger, IProtectiveAction[] protectiveActions)
        {
            UserKey = userKeyField;
            DeviceUid = deviceUid;
            ImmediateDanger = immediateDanger;
            ProtectiveActions = protectiveActions;
            this.Timestamp = DateTime.UtcNow;
        }

        public Key UserKey { get; set; }
        public string DeviceUid { get; set; }
        public ImmediateDangerDto ImmediateDanger { get; set; }
        public IProtectiveAction[] ProtectiveActions { get; set; }
    }
}
