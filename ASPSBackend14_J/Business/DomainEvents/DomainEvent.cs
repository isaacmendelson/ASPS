using Common;
using Common.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Business.DomainEvents
{
    public abstract class DomainEvent : IDomainEvent
    {


#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        protected DomainEvent()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
        }

        protected DomainEvent(DateTimeOffset utcTimestamp, string? userId = null,
            Key? tenantKey = null, IEventMetadata? cause = null)
        {
            this.Timestamp = utcTimestamp;
            this.UserId = userId;
            this.TenantKey = tenantKey;

            this.EventName = this.GetType().Name;

            this.Id = Guid.NewGuid();
            this.CorrelationId = cause?.CorrelationId ?? this.Id;
            this.CausationId = cause?.Id ?? this.Id;
        }

        protected DomainEvent(IDomainEventsContext context, string? name = null,
            Key? aggregateRootKey = null, string? aggregateVersion = null,
            Key? subjectKey = null)
        {
            _ = Guard.Against.Null(context);

            this.Timestamp = context.Timestamp;
            this.UserId = context.UserId;
            this.TenantKey = context.TenantKey;

            this.EventName = name ?? this.GetType().Name;

            this.AggregateRootKey = aggregateRootKey;
            this.AggregateRootVersion = aggregateVersion;

            this.SubjectKey = subjectKey;

            this.Id = Guid.NewGuid();
            this.CorrelationId = context.Cause?.CorrelationId ?? this.Id;
            this.CausationId = context.Cause?.Id ?? this.Id;
        }

        protected DomainEvent(Guid id, Guid correlationId, Guid causationId, DateTimeOffset timestamp,
            string? name = null, Key? subjectKey = null, Key? aggregateRootKey = null,
            string? aggregateVersion = null, Key? tenantKey = null, string? userId = null)
        {
            this.Timestamp = timestamp;
            this.UserId = userId;
            this.TenantKey = tenantKey;

            this.EventName = name ?? this.GetType().Name;

            this.AggregateRootKey = aggregateRootKey;
            this.AggregateRootVersion = aggregateVersion;

            this.Id = id;
            this.CorrelationId = correlationId;
            this.CausationId = causationId;
        }

        [Required]
        [DataMember(Order = 5)]
        public DateTimeOffset Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;

        [DataMember(Order = 6)] 
        public string? UserId { get; set; } = string.Empty;
        
        [DataMember(Order = 7)] 
        public Key? AggregateRootKey { get; set; }

        [DataMember(Order = 7)]
        public string? AggregateRootVersion { get; set; }

        [Required]
        [DataMember(Order = 1)]
        public Guid Id { get; private set; }

        [Required]
        [DataMember(Order = 2)]
        public Guid CorrelationId { get; private set; }


        [Required]
        [DataMember(Order = 3)]
        public Guid CausationId { get; private set; }

        [Required]
        [DataMember(Order = 4)]
        public string EventName { get; private set; }

        
        [DataMember(Order = 9)]
        public Key? TenantKey { get; protected set; }

        [DataMember(Order = 10)]
        public Key? SubjectKey { get; protected set; }

    }
}
