#nullable enable


using Common;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using System;
using System.Collections.Immutable;

namespace Business.DomainEvents
{
    public class AuditLogRecord
    {
        private PersistableDomainEvent? domainEvent;

        public AuditLogRecord(PersistableDomainEvent domainEvent)
        {
            _ = Guard.Against.Null(domainEvent);

            this.Id = domainEvent.Id;
            this.CorrelationId = domainEvent.CorrelationId;
            this.CausationId = domainEvent.CausationId;
            this.Timestamp = domainEvent.Timestamp.UtcDateTime;
            this.AggregateRootKeyField = domainEvent.AggregateRootKey?.ToString();
            this.AggregateRootVersion = domainEvent.AggregateRootVersion;
            this.SubjectKeyField = domainEvent.SubjectKey?.ToString();
            this.UserId = domainEvent.UserId;
            this.TenantKeyField = domainEvent.TenantKey?.ToString();
            this.EventName = domainEvent.EventName;
            this.DataField = JsonConvert.SerializeObject(domainEvent.Data);
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        protected AuditLogRecord()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
        }

        public PersistableDomainEvent? DomainEvent
        {
            get { return this.GetEvent(); }
        }

        public int KeyField { get; protected set; }

        public Guid Id { get; protected set; }

        public Guid CorrelationId { get; protected set; }

        public Guid CausationId { get; protected set; }

        public DateTime Timestamp { get; protected set; }

        public string? TenantKeyField { get; protected set; }

        public string? UserId { get; protected set; }

        public string? AggregateRootKeyField { get; protected set; }

        public string? AggregateRootVersion { get; protected set; }

        public string? SubjectKeyField { get; protected set; }

        public string EventName { get; protected set; }

        public string? DataField { get; protected set; }


        private PersistableDomainEvent GetEvent()
        {
            if (this.domainEvent is null)
            {
                var subjectKey = this.SubjectKeyField is null ? null : Key.Parse(this.SubjectKeyField);
                var aggRootKey = this.AggregateRootKeyField is null ? null : Key.Parse(this.AggregateRootKeyField);
                var tenantKey = this.TenantKeyField is null ? null : Key.Parse(this.TenantKeyField);
                var data = JsonConvert.DeserializeObject<ImmutableDictionary<string, object>>(this.DataField);
                this.domainEvent = new PersistableDomainEvent(this.Id, this.CorrelationId, this.CausationId,
                    this.Timestamp, this.EventName, subjectKey, aggRootKey, this.AggregateRootVersion,
                    data, tenantKey, this.UserId);
            }
            return this.domainEvent;
        }


        public class EFConfiguration : IEntityTypeConfiguration<AuditLogRecord>
        {
            public void Configure(EntityTypeBuilder<AuditLogRecord> builder)
            {
                builder.ToTable("AuditLog");
                builder.Property(i => i.KeyField).ValueGeneratedOnAdd();
                builder.HasKey(e => e.KeyField);
            }
        }
    }
}
