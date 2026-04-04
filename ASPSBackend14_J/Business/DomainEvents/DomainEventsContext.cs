#nullable enable

using Common;
using Common.Models;
using System;

namespace Business.DomainEvents
{
    public class DomainEventsContext : IDomainEventsContext
    {
        private readonly List<DomainEvent> events = new();

        public DomainEventsContext(Key companyKey, IEventMetadata? cause = null)
        {
            this.TenantKey = Guard.Against.Null(companyKey);
            this.Timestamp = DateTimeOffset.UtcNow;
            this.Cause = cause;
        }

        public DomainEventsContext(Key companyKey, string? userId, IEventMetadata? cause = null)
        {
            this.TenantKey = Guard.Against.Null(companyKey);
            this.UserId = userId;
            this.Timestamp = DateTimeOffset.UtcNow;
            this.Cause = cause;
        }

        public DateTimeOffset Timestamp { get; }

        public Key TenantKey { get; }

        public string? UserId { get; }

        public ClaimsPrincipalDto? Principal { get; set; }

        public IEventMetadata? Cause { get; }

        public IReadOnlyList<DomainEvent> Events
        {
            get { return this.events; }
        }

        public void AddEvent(DomainEvent e)
        {
            if (!this.events.Contains(e))
            {
                this.events.Add(e);
            }
        }
    }
}
