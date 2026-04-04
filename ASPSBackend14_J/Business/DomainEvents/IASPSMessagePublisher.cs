using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.DomainEvents
{
    public interface IASPSMessagePublisher 
    {
        void Publish(IASPSMessage message);
    }
}
