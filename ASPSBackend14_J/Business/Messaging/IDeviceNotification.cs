using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Messaging
{
    public interface IDeviceNotification
    {
        DateTime Timestamp { get; set; }
    }
}
