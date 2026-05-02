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
        protected ImmediateDangerEvent() { }
        
        public ImmediateDangerEvent(Key userKeyField, string deviceUid, object immediateDanger, IProtectiveAction[] protectiveActions)
        {
            UserKey = userKeyField;
            DeviceUid = deviceUid;
            ImmediateDanger = immediateDanger;
            ProtectiveActions = protectiveActions;
        }

        public Key UserKey { get; set; }
        public string DeviceUid { get; set; }
        public object ImmediateDanger { get; set; }
        public IProtectiveAction[] ProtectiveActions { get; set; }
    }
}
