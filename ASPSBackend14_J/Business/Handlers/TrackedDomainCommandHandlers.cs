using Business.Commands;
using Business.DomainEvents;
using Business.RealtimeAnalysis.UserDomain;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Interface.Repositories;
using Microsoft.Extensions.Logging;

namespace Business.Handlers;

/// <summary>
/// Command handlers for admin-managed TrackedDomains (ASPS-371).
/// Persists the row, then raises a SetTrackedDomains domain event through
/// the same DomainEventPublisher mechanism RealTimeAlertListener uses, so
/// NotificationPublisherActor (a registered IDomainEventHandler) fans the
/// list out to every device of the user → agent → Chrome extension.
/// </summary>
public class TrackedDomainCommandHandlers
{
    private readonly ITrackedDomainRepository _repository;
    private readonly ILogger<TrackedDomainCommandHandlers> _logger;
    private readonly DomainEventPublisher _domainEventPublisher;

    public TrackedDomainCommandHandlers(
        ITrackedDomainRepository repository,
        ILogger<TrackedDomainCommandHandlers> logger,
        IEnumerable<IDomainEventHandler> domainEventHandlers)
    {
        _repository = repository;
        _logger = logger;
        // Mirror RealTimeAlertListener: build a publisher subscribed to the
        // singleton handlers (NotificationPublisherActor, ASView, …).
        _domainEventPublisher = new DomainEventPublisher(domainEventHandlers);
    }

    public virtual async Task<AddTrackedDomainCommandResult> HandleAsync(AddTrackedDomainCommand command)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(command.Domain))
            {
                return new AddTrackedDomainCommandResult
                {
                    Success = false,
                    Message = "Domain is required."
                };
            }

            var category = string.IsNullOrWhiteSpace(command.Category)
                ? "Manual"
                : command.Category.Trim();

            // Idempotent: if it already exists, treat as success (re-sync only).
            var domainNorm = command.Domain.ToLowerInvariant().Trim();
            var existing = await _repository.GetByDomainAsync(domainNorm);
            int trackedDomainId;
            if (existing != null)
            {
                trackedDomainId = existing.Id;
                _logger.LogInformation(
                    "TrackedDomain '{Domain}' already exists (ID {Id}) — re-syncing only",
                    domainNorm, trackedDomainId);
            }
            else
            {
                var entity = new TrackedDomain(domainNorm, category); // ctor validates + lowercases
                trackedDomainId = await _repository.AddAsync(entity);
                _logger.LogInformation(
                    "Added TrackedDomain '{Domain}' (ID {Id}, Category {Category})",
                    domainNorm, trackedDomainId, category);
            }

            // Normalise the TrackMode int into the canonical enum (Phase 1).
            var trackMode = Enum.IsDefined(typeof(TrackMode), command.TrackMode)
                ? (TrackMode)command.TrackMode
                : TrackMode.Surf;

            var reason = string.IsNullOrWhiteSpace(command.Reason)
                ? "Admin added tracked domain"
                : command.Reason!;

            var trackedDomainCmd = new TrackedDomainCommand(
                domain: domainNorm,
                scamInProgressKey: command.ScamInProgressKey ?? string.Empty,
                trackMode: trackMode,
                reportType: ReportType.Backend,
                reason: reason);

            var evt = new SetTrackedDomains(
                userKeyField: command.UserKeyField ?? string.Empty,
                trackedDomains: new List<TrackedDomainCommand> { trackedDomainCmd },
                isCrossPlatformLock: false,
                reason: reason);

            // Register + RaiseAll synchronously (no await between them):
            // DomainEventPublisher uses a [ThreadStatic] queue, so both
            // calls must run on the same thread. The only await above has
            // already completed; this block is fully synchronous.
            _domainEventPublisher.Register(evt);
            _domainEventPublisher.RaiseAll();

            return new AddTrackedDomainCommandResult
            {
                Success = true,
                Message = existing != null
                    ? "Tracked domain already existed — re-synced to devices."
                    : "Tracked domain added and synced to devices.",
                TrackedDomainId = trackedDomainId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding tracked domain '{Domain}'", command.Domain);
            return new AddTrackedDomainCommandResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
