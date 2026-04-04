using Common.Models;
using System;
using System.Linq;

namespace Business.DomainEvents
{
    internal class AuditLogItem
    {
        public string Context { get; set; }

        public DateTimeOffset Timestamp { get; set; }

        public string Action { get; set; }

        public Key AggregateRootKey { get; set; }

        public string UserId { get; set; }

        public string Details { get; set; }

        public override string ToString()
        {
            return $"UTC={this.Timestamp:yyyy-MM-dd HH:mm:ss.fff}, User='{this.UserId}', Action='{this.Action}', {this.Details}";
        }

        public static AuditLogItem CreateItem(IDomainEvent domainEvent)
        {
            Type eventType = domainEvent.GetType();

            string details = string.Join(",",
            eventType.GetProperties()
                .Where(p => p.Name != nameof(DomainEvent.Timestamp))
                .Where(p => p.Name != nameof(DomainEvent.UserId))
                .Select(p =>
            {
                object value = p.GetGetMethod().Invoke(domainEvent, Array.Empty<object>());
                if (value == null)
                {
                    return p.Name + "=" + "<null>";
                }
                return p.Name + "=[" + value.ToString() + "]";
            }));

            return new AuditLogItem()
            {
                UserId = domainEvent.UserId,
                Timestamp = domainEvent.Timestamp,
                AggregateRootKey = domainEvent.AggregateRootKey,
                Action = eventType.Name,
                Context = eventType.Namespace,
                Details = details,
            };
        }

    }
}
