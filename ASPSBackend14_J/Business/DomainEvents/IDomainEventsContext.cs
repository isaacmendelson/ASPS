#nullable enable

using Common;
using Common.Models;
using System;

namespace Business.DomainEvents
{
    public interface IDomainEventsContext
    {
        Key TenantKey { get; }

        DateTimeOffset Timestamp { get; }

        string? UserId { get; }

        IEventMetadata? Cause { get; }

        ClaimsPrincipalDto? Principal { get; }
    }
}
