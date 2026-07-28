using Interface.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Business.Services;

/// <summary>
/// Periodic background service that prunes acknowledged outbox notifications.
/// Runs on a configurable interval (default 1 hour) so the NotificationOutbox
/// table does not grow unbounded.
/// ASPS-620 — Major-3 fix: ensures PruneAcknowledgedAsync is actually called.
/// </summary>
public class OutboxPruningService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPruningService> _logger;
    private readonly TimeSpan _pruneInterval;
    private readonly TimeSpan _retentionPeriod;

    public OutboxPruningService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPruningService> logger,
        TimeSpan? pruneInterval = null,
        TimeSpan? retentionPeriod = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pruneInterval = pruneInterval ?? TimeSpan.FromHours(1);
        _retentionPeriod = retentionPeriod ?? TimeSpan.FromDays(7);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "[OutboxPruning] Service started — interval={Interval}, retention={Retention}",
            _pruneInterval, _retentionPeriod);

        // Wait one full interval before first prune so startup is not affected.
        await Task.Delay(_pruneInterval, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<INotificationOutboxRepository>();
                await repo.PruneAcknowledgedAsync(_retentionPeriod);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[OutboxPruning] Error during prune cycle");
            }

            await Task.Delay(_pruneInterval, stoppingToken);
        }

        _logger.LogInformation("[OutboxPruning] Service stopping");
    }
}
