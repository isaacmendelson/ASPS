#nullable enable
using Common;
using Common.Models;
using System;

namespace Business.DomainEvents
{
    public interface IDomainEvent
    {
        DateTimeOffset Timestamp { get; }

        string? UserId { get; }

        Key? AggregateRootKey { get; }

        string? AggregateRootVersion { get; }
    }
}
