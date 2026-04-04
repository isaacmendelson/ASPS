
using System;

namespace Business.DomainEvents
{
	public interface IDomainEventHandler
	{
	    Task Handle(IDomainEvent evt);
        Type[] GetHandleableEvents();
        
    }
}
