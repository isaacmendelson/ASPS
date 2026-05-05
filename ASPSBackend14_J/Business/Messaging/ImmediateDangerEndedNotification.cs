using Common.Interfaces;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Business.Messaging
{
    [DataContract]
    public class ImmediateDangerEndedNotification : DeviceNotification
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        protected ImmediateDangerEndedNotification()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
        }

        public ImmediateDangerEndedNotification(Key immediateDangerKey, Key userKey, string deviceUid, DateTime endTime, IProtectiveAction[] protectiveActions)
        {
            ImmediateDangerKey = immediateDangerKey;
            UserKey = userKey;
            DeviceUid = deviceUid;
            ProtectiveActions = protectiveActions;
            EndTime = endTime;
            Timestamp = DateTime.UtcNow;
        }

        [DataMember]
        public Key ImmediateDangerKey { get; set; }

        [DataMember]
        public Key UserKey { get; set; }
        [DataMember]
        public string DeviceUid { get; set; }
        
        [DataMember]
        public DateTime EndTime { get; set; }
        [DataMember]
        public IProtectiveAction[] ProtectiveActions { get; set; }
    }
}
