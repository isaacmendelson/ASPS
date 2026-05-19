using Business.Commands;
using Business.DomainEvents;
using Business.RealtimeAnalysis.UserDomain;
using Business.Views;
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
    private readonly ASView _asView;
    private readonly DomainEventPublisher _domainEventPublisher;

    public TrackedDomainCommandHandlers(
        ITrackedDomainRepository repository,
        ILogger<TrackedDomainCommandHandlers> logger,
        ASView asView,
        IEnumerable<IDomainEventHandler> domainEventHandlers)
    {
        _repository = repository;
        _logger = logger;
        _asView = asView;
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

            var domainNorm = command.Domain.ToLowerInvariant().Trim();
            int? trackedDomainId = null;
            bool existedAlready = false;

            // NotifyOnly: admin "Notify…" action on an existing row — do NOT
            // (re-)persist, just re-raise the event.
            if (!command.NotifyOnly)
            {
                var existing = await _repository.GetByDomainAsync(domainNorm);
                if (existing != null)
                {
                    existedAlready = true;
                    trackedDomainId = existing.Id;
                    _logger.LogInformation(
                        "TrackedDomain '{Domain}' already exists (ID {Id}) — re-syncing only",
                        domainNorm, trackedDomainId);
                }
                else
                {
                    var entity = new TrackedDomain(
                        domainNorm,
                        category,
                        userKey: command.UserKeyField,
                        scamInProgressKey: command.ScamInProgressKey,
                        trackMode: command.TrackMode,
                        reason: command.Reason); // ctor validates + lowercases
                    trackedDomainId = await _repository.AddAsync(entity);
                    _logger.LogInformation(
                        "Added TrackedDomain '{Domain}' (ID {Id}, Category {Category})",
                        domainNorm, trackedDomainId, category);
                }
            }

            // ── From here down it is fully synchronous (no await) so the
            //    DomainEventPublisher [ThreadStatic] queue stays on one thread.
            var trackMode = Enum.IsDefined(typeof(TrackMode), command.TrackMode)
                ? (TrackMode)command.TrackMode
                : TrackMode.Surf;

            var reason = string.IsNullOrWhiteSpace(command.Reason)
                ? "Admin tracked domain"
                : command.Reason!;

            TrackedDomainCommand BuildCmd() => new TrackedDomainCommand(
                domain: domainNorm,
                scamInProgressKey: command.ScamInProgressKey ?? string.Empty,
                trackMode: trackMode,
                reportType: ReportType.Backend,
                reason: reason);

            int eventCount;
            if (command.NotifyAllUsers)
            {
                // Fan out to EVERY user's devices: one SetTrackedDomains per
                // user (NotificationPublisherActor resolves devices per event).
                var userKeys = _asView.GetUsers()
                    .Select(u => u.KeyField)
                    .Where(k => !string.IsNullOrEmpty(k))
                    .Distinct()
                    .ToList();
                foreach (var uk in userKeys)
                {
                    _domainEventPublisher.Register(new SetTrackedDomains(
                        userKeyField: uk!,
                        trackedDomains: new List<TrackedDomainCommand> { BuildCmd() },
                        isCrossPlatformLock: false,
                        reason: reason));
                }
                eventCount = userKeys.Count;
            }
            else
            {
                _domainEventPublisher.Register(new SetTrackedDomains(
                    userKeyField: command.UserKeyField ?? string.Empty,
                    trackedDomains: new List<TrackedDomainCommand> { BuildCmd() },
                    isCrossPlatformLock: false,
                    reason: reason));
                eventCount = 1;
            }

            _domainEventPublisher.RaiseAll();

            var verb = command.NotifyOnly
                ? (command.NotifyAllUsers ? $"Re-notified all users ({eventCount} users)."
                                          : "Re-notified user devices.")
                : (existedAlready ? "Tracked domain already existed — re-synced to devices."
                                  : "Tracked domain added and synced to devices.");

            return new AddTrackedDomainCommandResult
            {
                Success = true,
                Message = verb,
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

    public virtual async Task<UpdateTrackedDomainCommandResult> HandleAsync(UpdateTrackedDomainCommand command)
    {
        try
        {
            var entity = await _repository.GetByIdAsync(command.Id);
            if (entity == null)
            {
                return new UpdateTrackedDomainCommandResult
                {
                    Success = false,
                    Message = $"Tracked domain ID {command.Id} not found."
                };
            }

            var domainNorm = string.IsNullOrWhiteSpace(command.Domain)
                ? entity.Domain
                : command.Domain.ToLowerInvariant().Trim();
            var category = string.IsNullOrWhiteSpace(command.Category)
                ? entity.Category
                : command.Category.Trim();

            entity.Domain = domainNorm;
            entity.Category = category;
            entity.UserKey = string.IsNullOrWhiteSpace(command.UserKeyField) ? null : command.UserKeyField!.Trim();
            entity.ScamInProgressKey = string.IsNullOrWhiteSpace(command.ScamInProgressKey) ? null : command.ScamInProgressKey!.Trim();
            entity.TrackMode = command.TrackMode;
            entity.IsActive = command.IsActive;
            entity.Reason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason!.Trim();

            await _repository.UpdateAsync(entity);

            // ── Synchronous from here (ThreadStatic publisher queue). ──
            var reason = string.IsNullOrWhiteSpace(command.Reason)
                ? "Admin edited tracked domain"
                : command.Reason!;
            var trackMode = Enum.IsDefined(typeof(TrackMode), entity.TrackMode)
                ? (TrackMode)entity.TrackMode
                : TrackMode.Surf;

            // 1) Audit/extension-point event for the edit.
            _domainEventPublisher.Register(new TrackedDomainChanged(
                entity.Id, entity.Domain, entity.Category,
                entity.UserKey, entity.ScamInProgressKey, entity.TrackMode, reason));

            // 2) Re-sync the (now edited) domain to the owning user's devices,
            //    same pipeline as Add — so the change actually reaches agents.
            _domainEventPublisher.Register(new SetTrackedDomains(
                userKeyField: entity.UserKey ?? string.Empty,
                trackedDomains: new List<TrackedDomainCommand>
                {
                    new TrackedDomainCommand(
                        domain: entity.Domain,
                        scamInProgressKey: entity.ScamInProgressKey ?? string.Empty,
                        trackMode: trackMode,
                        reportType: ReportType.Backend,
                        reason: reason)
                },
                isCrossPlatformLock: false,
                reason: reason));

            _domainEventPublisher.RaiseAll();

            _logger.LogInformation("Tracked domain ID {Id} updated ('{Domain}')", entity.Id, entity.Domain);
            return new UpdateTrackedDomainCommandResult
            {
                Success = true,
                Message = "Tracked domain updated and re-synced to devices.",
                TrackedDomainId = entity.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tracked domain ID {Id}", command.Id);
            return new UpdateTrackedDomainCommandResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public virtual async Task<DeleteTrackedDomainCommandResult> HandleAsync(DeleteTrackedDomainCommand command)
    {
        try
        {
            var entity = await _repository.GetByIdAsync(command.Id);
            if (entity == null)
            {
                return new DeleteTrackedDomainCommandResult
                {
                    Success = false,
                    Message = $"Tracked domain ID {command.Id} not found."
                };
            }

            var domain = entity.Domain;
            var userKey = entity.UserKey;

            await _repository.DeleteAsync(command.Id); // soft delete

            // ── Synchronous from here (ThreadStatic publisher queue). ──
            var reason = string.IsNullOrWhiteSpace(command.Reason)
                ? "Admin deleted tracked domain"
                : command.Reason!;
            _domainEventPublisher.Register(new TrackedDomainDeleted(command.Id, domain, userKey, reason));
            _domainEventPublisher.RaiseAll();

            _logger.LogInformation("Tracked domain ID {Id} deleted ('{Domain}')", command.Id, domain);
            return new DeleteTrackedDomainCommandResult
            {
                Success = true,
                Message = "Tracked domain deleted."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tracked domain ID {Id}", command.Id);
            return new DeleteTrackedDomainCommandResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
