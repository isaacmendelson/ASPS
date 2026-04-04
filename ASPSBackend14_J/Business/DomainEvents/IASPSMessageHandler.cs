using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.DomainEvents
{
    internal interface IASPSMessageHandler 
    {
        Type[] GetHandleableMessageTypes();
        void Handle(IASPSMessage message);
    }
}
