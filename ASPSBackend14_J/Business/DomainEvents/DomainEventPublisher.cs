#nullable enable
using NLog;

using System.Composition;

namespace Business.DomainEvents
{
    [Export(typeof(IDomainEventPublisher)), Shared]
    internal class DomainEventPublisher : IDomainEventPublisher
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        [ThreadStatic]
        private static Queue<IDomainEvent> _events;



        private readonly Dictionary<Type, List<IDomainEventHandler>> handlers = new Dictionary<Type, List<IDomainEventHandler>>();

        [ImportingConstructor]
        public DomainEventPublisher([ImportMany]IEnumerable<IDomainEventHandler> handlers)
        {
            foreach (var handler in handlers)
            {
                this.Subscribe(handler);
            }
        }

        public void Subscribe(IDomainEventHandler handler)
        {
            foreach (var type in handler.GetHandleableEvents())
            {
                if (!this.handlers.TryGetValue(type, out List<IDomainEventHandler> handlersList))
                {
                    handlersList = new List<IDomainEventHandler>();
                    this.handlers.Add(type, handlersList);
                }
                handlersList.Add(handler);
            }
        }

        public void Register(IDomainEvent evt)
        {
            _events ??= new();
            _events.Enqueue(evt);
        }

        public void Register(IEnumerable<IDomainEvent> events)
        {
            _events ??= new();

            foreach (var e in events)
            {
                _events.Enqueue(e);
            }
        }

        public void RaiseAll()
        {
            _events ??= new();

            while (_events.Count > 0)
            {
                IDomainEvent e = _events.Dequeue();

                var auditLogItem = AuditLogItem.CreateItem(e);
                if (auditLogItem.UserId != null)
                {
                    _logger.Info(auditLogItem);
                }
                else
                {
                    _logger.Trace(auditLogItem);
                }

                if (this.handlers.TryGetValue(e.GetType(), out List<IDomainEventHandler> handlersList))
                {
                    foreach (var handler in handlersList.Where(i => i.GetHandleableEvents().ToList().Contains(e.GetType())))
                    {
                        try
                        {
                            handler.Handle(e);
                        }
                        catch (Exception x)
                        {
                            _logger.Error(x, "DomainEventHandler '{Handler}' failed.", handler.GetType());
                        }
                    }
                }
            }
        }

        
    }
}
