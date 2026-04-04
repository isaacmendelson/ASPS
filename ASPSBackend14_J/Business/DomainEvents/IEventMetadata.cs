#nullable enable

using System;

namespace Business.DomainEvents
{
    public interface IEventMetadata
    {
        Guid Id { get; }
        Guid CorrelationId { get; }
    }
}
