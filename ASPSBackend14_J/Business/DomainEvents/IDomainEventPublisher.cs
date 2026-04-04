using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.DomainEvents
{
    internal interface IDomainEventPublisher 
    {
        void Register(IDomainEvent evt);
        void Register(IEnumerable<IDomainEvent> events);
        void RaiseAll();
        void Subscribe(IDomainEventHandler handler);
    }
}
