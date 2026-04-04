#nullable enable

using Common;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Business.DomainEvents
{
    [DataContract]
    public class PersistableDomainEvent : DomainEvent
    {
        public PersistableDomainEvent(IDomainEventsContext context, string name,
            IDictionary<string, object?>? data, Key? subjectKey, 
            Key? aggregateRootKey = null, string? aggregateVersion = null)
            : base(context, name, aggregateRootKey, aggregateVersion, subjectKey)
        {
            this.Data = data?.ToImmutableDictionary() ?? ImmutableDictionary<string, object?>.Empty;
        }

        public PersistableDomainEvent(Guid id, Guid correlationId, Guid causationId, 
            DateTimeOffset timestamp, string name,
            Key? subjectKey, Key? aggregateRootKey, string? aggregateVersion,
            IDictionary<string, object?>? data, Key? tenantKey, string? userId)
            : base(id, correlationId, causationId, timestamp, name, subjectKey, aggregateRootKey,
                  aggregateVersion, tenantKey, userId)
        {
            this.Data = data?.ToImmutableDictionary() ?? ImmutableDictionary<string, object?>.Empty;
        }

        protected PersistableDomainEvent()
        {
            this.Data = ImmutableDictionary<string, object?>.Empty;
        }

        [IgnoreDataMember]
        public ImmutableDictionary<string, object?> Data { get; private set; }

        public bool TryGetObject<T>(string key, out T? value) where T : class
        {
            if (this.Data.TryGetValue(key, out object? obj) && obj is T tvalue)
            {
                value = tvalue;
                return true;
            }

            value = null;
            return false;
        }

        public bool TryGetValue<T>(string key, out T value) where T : struct
        {
            if (this.Data.TryGetValue(key, out object? obj) && obj is T tvalue)
            {
                value = tvalue;
                return true;
            }

            value = default;
            return false;
        }
 

         
            
    }
}
