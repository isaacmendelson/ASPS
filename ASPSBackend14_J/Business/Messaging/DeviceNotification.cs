using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Business.Messaging
{
    [DataContract]
    public class DeviceNotification : IDeviceNotification
    {
        [DataMember]
        public DateTime Timestamp { get; set; }
    }
}
