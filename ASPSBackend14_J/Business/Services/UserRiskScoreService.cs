using System.Collections.Concurrent;
using System.Text.Json;
using Business.Data.EF;
using Business.DomainEvents;
using Business.RealtimeAnalysis.UserDomain;
using Common.Entities;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Business.Services;

/// <summary>
/// Orchestrates User Risk Score computation. SCRUM-904 Phase 1 MVP wrap-up:
/// listens to <see cref="DeviceAlertReceived"/>, throttles per-user
/// recomputes, snapshots each computed <see cref="UserRiskScore"/> into the
/// <see cref="UserRiskScoreHistory"/> table for trend / replay / explainability.
///
/// Realtime policy (per the user's decision in design §10 #2):
/// - An alert carrying <c>DeviceInfo.ImmediateDanger == true</c> bypasses the
///   throttle and triggers an immediate recompute.
/// - Other alerts respect a per-user cooldown (<see cref="ThrottleWindow"/>);
///   the periodic batch path (future hosted-service) handles freshness for
///   users whose alerts don't naturally tickle the system inside the window.
/// </summary>
public class UserRiskScoreService : IDomainEventHandler
{
    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromSeconds(60);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UserRiskScoreService> _logger;

    /// <summary>Last-recomputed timestamp per user; used by the throttle.</summary>
    private readonly ConcurrentDictionary<string, DateTime> _lastComputed = new();

    public UserRiskScoreService(
        IServiceProvider serviceProvider,
        ILogger<UserRiskScoreService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Type[] GetHandleableEvents() => new[] { typeof(DeviceAlertReceived) };

    public async Task Handle(IDomainEvent evt)
    {
        if (evt is DeviceAlertReceived alertReceived)
            await HandleDeviceAlertReceivedAsync(alertReceived);
    }

    public virtual async Task HandleDeviceAlertReceivedAsync(DeviceAlertReceived alertReceived)
    {
        var alert = alertReceived.Alert;
        if (alert?.DeviceInfo == null) return;

        // User key may be set on the alert's DeviceInfo (set upstream by
        // AlertPersistenceActor against the in-memory ASView). If absent we
        // cannot scope URS to a user.
        var userKey = alert.DeviceInfo.UserKey?.Value;
        if (string.IsNullOrEmpty(userKey))
            return;

        // Live signals bypass the throttle — the user's decision #2 in §10.
        var isLive = alert.DeviceInfo.ImmediateDanger == true;
        if (!isLive && IsThrottled(userKey))
            return;

        try
        {
            await ComputeAndPersistAsync(userKey);
        }
        catch (Exception ex)
        {
            // Per the team charter: never let a background failure mask the
            // primary alert path. Log + swallow.
            _logger.LogError(ex, "URS recompute failed for user {UserKey}", userKey);
        }
    }

    /// <summary>
    /// Run the URS calculator for a user (with their current consent) and
    /// persist the result as a <see cref="UserRiskScoreHistory"/> snapshot.
    /// Returns the computed score (same object stored in the snapshot's
    /// SerializedSnapshot JSON).
    /// </summary>
    public virtual async Task<UserRiskScore> ComputeAndPersistAsync(string userKey)
    {
        using var scope = _serviceProvider.CreateScope();
        var calculator = scope.ServiceProvider.GetRequiredService<UserRiskScoreCalculator>();
        var consentService = scope.ServiceProvider.GetRequiredService<ConsentService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var consent = await consentService.GetAllAsync(userKey);
        var urs = await calculator.CalculateAsync(userKey, consent);

        var snapshot = new UserRiskScoreHistory
        {
            UserKey = userKey,
            Score = urs.Score,
            Level = urs.Level,
            Confidence = urs.Confidence,
            ComputedAt = urs.ComputedAt,
            VulnerabilityScore = urs.AxisScores.VulnerabilityScore,
            ExposureScore = urs.AxisScores.ExposureScore,
            LiveScore = urs.AxisScores.LiveScore,
            CorrelationScore = urs.AxisScores.CorrelationScore,
            SerializedSnapshot = JsonSerializer.Serialize(urs),
        };
        db.UserRiskScoreHistories.Add(snapshot);
        await db.SaveChangesAsync();

        _lastComputed[userKey] = DateTime.UtcNow;
        _logger.LogInformation(
            "URS for user {UserKey}: Score={Score} Level={Level} Confidence={Confidence:0.##}",
            userKey, urs.Score, urs.Level, urs.Confidence);

        return urs;
    }

    /// <summary>
    /// Read the most recent persisted <see cref="UserRiskScore"/> for a user,
    /// or null if none has been computed yet. Deserializes from the
    /// SerializedSnapshot column so the full structured object is restored.
    /// </summary>
    public virtual async Task<UserRiskScore?> GetLatestAsync(string userKey)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var latest = await db.UserRiskScoreHistories
            .Where(h => h.UserKey == userKey)
            .OrderByDescending(h => h.ComputedAt)
            .FirstOrDefaultAsync();
        if (latest == null) return null;

        try
        {
            return JsonSerializer.Deserialize<UserRiskScore>(latest.SerializedSnapshot);
        }
        catch (JsonException ex)
        {
            // The snapshot column was supposed to be authoritative — log loudly
            // if it cannot round-trip back into a UserRiskScore.
            _logger.LogError(ex,
                "Could not deserialize UserRiskScoreHistory row Id for user {UserKey}", userKey);
            return null;
        }
    }

    private bool IsThrottled(string userKey)
    {
        if (!_lastComputed.TryGetValue(userKey, out var last))
            return false;
        return DateTime.UtcNow - last < ThrottleWindow;
    }
}
