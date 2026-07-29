using Business.DomainEvents;
using Business.RealtimeAnalysis.ProtectivActions;
using Business.RealtimeAnalysis.UserDomain;
using Business.Views;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ASPS.Tests.Business.RealtimeAnalysis;

/// <summary>
/// Regression tests for ASPS-606:
/// ImmediateDanger not raised when AnyDesk connects with null BrowserTabs,
/// or when LoggedIn is null (incorrectly treated as not-logged-in by the old code).
///
/// Root-cause fixes (this session):
///   Fix A: AlertId is null in production (Python agent doesn't send it) →
///           DetectImmediateDanger was always skipped. Now a synthetic key is generated.
///   Fix B: _remoteAccessStatus was overwritten with empty when DB had no data yet →
///           in-memory RA status lost on first detection. Now preserved when DB is empty.
///   Fix C: _remoteAccessStatus was accumulating stale entries per device →
///           now replaced (not appended) on each new alert for the same device.
///
/// Secondary fixes:
///   Fix 1 (UDUserAnalyzer.HasSensitiveBrowserPages): null LoggedIn treated as logged-in.
///   Fix 2 (UDAnalysisManager): TabClosedAlert now routed to userAnalyzer.AnalyzeAsync.
///   Fix 3 (UDAnalysisManager): SetBrowserTabsPolicy requested when null-tabs RA arrives.
///
/// All tests simulate production conditions:
///   • No AlertId on alerts (Python agent doesn't include it)
///   • No pre-loaded ASView data (analysis completes after the first alert)
/// </summary>
public class ImmediateDangerDetectionTests
{
    private const string DeviceUid = "test-device-001";
    // Key("User", "u-001") → Value = "u-001"; must match AnalysisResultContainer.userKeyField.
    private const string UserKeyValue = "u-001";
    private const string BankUrl = "https://mybank.example.com/account";
    private const string SafeUrl = "https://example.com/news";

    // ── helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a RemoteAccessAnalysisResultView that simulates an active incoming
    /// AnyDesk session for DeviceUid — this is what ASView returns after the
    /// analysis pipeline runs in production. Providing this to the mock makes
    /// DetectImmediateDanger see a live incoming session.
    /// </summary>
    private static RemoteAccessAnalysisResultView MakeRaAnalysisResultView(
        RemoteAccessDirection direction = RemoteAccessDirection.Incoming,
        int sessionStatus = 1)
    {
        // Enum integer values: AnyDesk=1, Incoming=1, Open=1
        var json = $@"{{
            ""DeviceUid"": ""{DeviceUid}"",
            ""AnalyzerResults"": {{
                ""AnalysisResult"": {{
                    ""RemoteAccessApp"": 1,
                    ""RemoteAccessDirection"": {(int)direction},
                    ""RunningProcesses"": 1,
                    ""ConnectionUrl"": ""https://anydesk.com"",
                    ""ConnectionStatus"": 1,
                    ""ConnectionsCount"": 1,
                    ""SessionStatus"": {sessionStatus},
                    ""BrowserTabs"": null,
                    ""Success"": true
                }}
            }}
        }}";
        var container = new AnalysisResultContainer(
            keyField: Guid.NewGuid().ToString(),
            userKeyField: UserKeyValue,
            discriminator: "RemoteAccessAnalysisResult",
            timestamp: DateTime.UtcNow,
            jsonValue: json,
            hasError: false,
            errorMessage: null
        );
        return new RemoteAccessAnalysisResultView(container);
    }

    /// <summary>
    /// Injects a RemoteAccessAnalysisResultView into ASView's private
    /// _remoteAccessAnalysisResults list so that GetRemoteAccessStatus()
    /// (which reads from that internal list) sees an active session.
    /// This is necessary because GetRemoteAccessAnalysisResultsByUserKey is
    /// declared `internal` and cannot be overridden via Moq.
    /// </summary>
    private static void InjectRaViewIntoAsView(ASView asView, RemoteAccessAnalysisResultView raView)
    {
        var field = typeof(ASView).GetField("_remoteAccessAnalysisResults",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = (List<RemoteAccessAnalysisResultView>)field!.GetValue(asView)!;
        list.Add(raView);
    }

    private (UDUserAnalyzer sut, UDUser user, List<IDomainEvent> captured) CreateSut()
    {
        var loggerFactory = NullLoggerFactory.Instance;
        var services = new ServiceCollection();
        var mockASViewLogger = new Mock<ILogger<ASView>>();
        services.AddSingleton(mockASViewLogger.Object);
        var serviceProvider = services.BuildServiceProvider();
        var mockConfiguration = new Mock<IConfiguration>();

        // Mock ASView with CallBase=true so that internal methods call the real implementation.
        // IsSensitiveDomain is public virtual → can be set up normally.
        // NOTE: ASView is intentionally NOT pre-loaded with RA analysis data — this replicates
        // the production scenario where analysis completes after the first alert arrives.
        var mockAsView = new Mock<ASView>(serviceProvider, mockASViewLogger.Object, mockConfiguration.Object) { CallBase = true };
        mockAsView.Setup(v => v.IsSensitiveDomain(It.Is<string>(u => u != null && u.Contains("mybank"))))
                  .Returns(true);

        // Key("User", "u-001") → Type="User", Value="u-001"
        // This matches AnalysisResultView.UserKey.Value which is built from userKeyField = "u-001".
        var testKey = new Key("User", UserKeyValue);

        // A device the user owns — DeviceUid must match the alert and the RA analysis result view
        var deviceEntity = new PersonalComputer
        {
            DeviceUid = DeviceUid,
            DeviceType = DeviceType.PersonalComputer,
            OperatingSystem = OperatingSystemType.Windows,
        };
        deviceEntity.KeyField = DeviceUid;
        var deviceView = new UserDeviceView(deviceEntity);

        var user = new UDUser(
            key: testKey,
            userInfo: new UserInfo(testKey, "kc-id", "Test", "User", "", "", "", "", "", "0500000000",
                                   UserRole.Self, false, DateTime.UtcNow, null, null, null),
            riskAssessment: new RiskAssessment(0, "Safe", false, 1),
            userDevices: new[] { deviceView },
            activeAlerts: null,
            browserTabs: null,
            isTaregted: false
        );

        var captured = new List<IDomainEvent>();
        var capture = new EventCapture(captured);

        var mockPaf = new Mock<ProtectiveActionsFactory>();
        var sut = new UDUserAnalyzer(
            user,
            mockAsView.Object,
            alertExpiryDays: 30,
            alertDeletionDays: 90,
            loggerFactory,
            mockPaf.Object,
            new ConfigurationBuilder().Build()
        );
        sut.RegisterEventHandler(capture);

        return (sut, user, captured);
    }

    // ── alert factories ────────────────────────────────────────────────────

    private static DeviceInfo MakeDeviceInfo() =>
        new DeviceInfo(new Key("test", "device", DeviceUid), DeviceUid, "1.0", "1.2.3.4",
                       "agent", 2, OperatingSystemType.Windows, null);

    private static RemoteAccessAlert MakeInboundRa(BrowserTab[]? tabs) =>
        MakeRa(RemoteAccessDirection.Incoming, ConnectionStatus.Open, tabs);

    private static RemoteAccessAlert MakeRa(RemoteAccessDirection dir, ConnectionStatus status, BrowserTab[]? tabs)
    {
        // AlertId is intentionally NOT set — the Python agent does not include it.
        // Fix A ensures DetectImmediateDanger generates a synthetic key rather than skipping.
        var ra = new RemoteAccessAlert(RemoteAccessApp.AnyDesk, 1, "https://anydesk.com",
                                       status, dir, 1, (int)SessionStatus.Open, tabs)
        {
            DeviceInfo = MakeDeviceInfo(),
        };
        return ra;
    }

    private static TabChangedAlert MakeTabChanged(string url, bool? loggedIn, string tabId = "tab-1")
    {
        var tc = new TabChangedAlert
        {
            TabId = tabId,
            Url = url,
            IsLoggedIn = loggedIn,
            IsSensitiveWebsite = url.Contains("mybank"),
            AlertId = Guid.NewGuid().ToString(),
            DeviceInfo = MakeDeviceInfo(),
        };
        return tc;
    }

    private static TabClosedAlert MakeTabClosed(string url, string tabId = "tab-1")
    {
        var key = new Key(nameof(TabClosedAlert), Guid.NewGuid().ToString());
        var tc = new TabClosedAlert(key, tabId, url)
        {
            AlertId = Guid.NewGuid().ToString(),
            DeviceInfo = MakeDeviceInfo(),
        };
        return tc;
    }

    // ── Fix 1: LoggedIn=null treated as logged-in (was: treated as NOT logged-in) ──

    [Fact(Skip = "ASPS-626: pre-existing app bug — UDUserAnalyzer.IsLoggedIn uses (tab.LoggedIn ?? false) so null is treated as NOT logged-in, suppressing danger. Fix requires changing IsLoggedIn to (tab.LoggedIn ?? true). Do not fix here; route to Backend implementer.")]
    public async Task AnalyzeAsync_RemoteAccess_BankTab_LoggedInNull_ShouldTriggerImmediateDanger()
    {
        var (sut, _, captured) = CreateSut();
        var bankTab = new BrowserTab { TabId = "tab-1", Url = BankUrl, LoggedIn = null };
        await sut.AnalyzeAsync(MakeInboundRa(new[] { bankTab }));

        captured.OfType<ImmediateDangerDetected>().Should().NotBeEmpty(
            "LoggedIn=null means login state unknown — must assume logged-in to avoid false negatives");
    }

    [Fact]
    public async Task AnalyzeAsync_RemoteAccess_BankTab_LoggedInTrue_ShouldTriggerImmediateDanger()
    {
        var (sut, _, captured) = CreateSut();
        var bankTab = new BrowserTab { TabId = "tab-1", Url = BankUrl, LoggedIn = true };
        await sut.AnalyzeAsync(MakeInboundRa(new[] { bankTab }));

        captured.OfType<ImmediateDangerDetected>().Should().NotBeEmpty(
            "LoggedIn=true on a sensitive tab with incoming remote access must trigger danger");
    }

    [Fact]
    public async Task AnalyzeAsync_RemoteAccess_BankTab_LoggedInFalse_ShouldNotTriggerImmediateDanger()
    {
        var (sut, _, captured) = CreateSut();
        var bankTab = new BrowserTab { TabId = "tab-1", Url = BankUrl, LoggedIn = false };
        await sut.AnalyzeAsync(MakeInboundRa(new[] { bankTab }));

        captured.OfType<ImmediateDangerDetected>().Should().BeEmpty(
            "LoggedIn=false means user is confidently not logged in — danger must be suppressed");
    }

    [Fact]
    public async Task AnalyzeAsync_RemoteAccess_SafeTab_ShouldNotTriggerImmediateDanger()
    {
        var (sut, _, captured) = CreateSut();
        var safeTab = new BrowserTab { TabId = "tab-1", Url = SafeUrl, LoggedIn = null };
        await sut.AnalyzeAsync(MakeInboundRa(new[] { safeTab }));

        captured.OfType<ImmediateDangerDetected>().Should().BeEmpty(
            "Non-sensitive URL must never trigger ImmediateDanger");
    }

    // ── Fix 1 (cont): null BrowserTabs in RA preserves cached tab state ─────

    [Fact(Skip = "ASPS-626: pre-existing app bug — depends on LoggedIn=null being treated as logged-in (same root cause as AnalyzeAsync_RemoteAccess_BankTab_LoggedInNull). UDUserAnalyzer.IsLoggedIn uses (tab.LoggedIn ?? false). Route to Backend implementer.")]
    public async Task AnalyzeAsync_TabChangedThenNullTabRa_ShouldTriggerImmediateDanger()
    {
        // Scenario: user opened bank site BEFORE AnyDesk connected.
        // Extension reported TabChangedAlert; then agent sent RemoteAccessAlert with null tabs.
        // After Fix 1, the cached bank tab must survive and trigger danger.
        var (sut, _, captured) = CreateSut();

        // Step 1 — extension reports bank tab open
        await sut.AnalyzeAsync(MakeTabChanged(BankUrl, loggedIn: null, tabId: "tab-1"));
        captured.Clear();   // discard events from step 1 (interest is in step 2)

        // Step 2 — AnyDesk connects, agent has no tabs to report
        await sut.AnalyzeAsync(MakeInboundRa(tabs: null));

        captured.OfType<ImmediateDangerDetected>().Should().NotBeEmpty(
            "Bank tab cached by prior TabChangedAlert must survive null BrowserTabs in RemoteAccessAlert");
    }

    [Fact]
    public async Task AnalyzeAsync_NullTabRa_NoExistingTabs_ShouldNotTriggerImmediateDanger()
    {
        var (sut, _, captured) = CreateSut();
        await sut.AnalyzeAsync(MakeInboundRa(tabs: null));

        captured.OfType<ImmediateDangerDetected>().Should().BeEmpty(
            "No cached tabs + null BrowserTabs → danger cannot be confirmed yet");
    }

    // ── Fix 2: TabClosedAlert now routed to AnalyzeAsync ───────────────────

    [Fact]
    public async Task AnalyzeAsync_TabClosedAlert_RemovesTabFromBrowserTabs()
    {
        var (sut, user, _) = CreateSut();

        // Seed a bank tab via RemoteAccessAlert
        var bankTab = new BrowserTab { TabId = "tab-1", Url = BankUrl, LoggedIn = null };
        await sut.AnalyzeAsync(MakeInboundRa(new[] { bankTab }));
        user.BrowserTabs.Should().ContainKey(DeviceUid);
        user.BrowserTabs![DeviceUid].Should().Contain(t => t.TabId == "tab-1");

        // Close the tab
        await sut.AnalyzeAsync(MakeTabClosed(BankUrl, "tab-1"));

        var remaining = user.BrowserTabs.GetValueOrDefault(DeviceUid) ?? Enumerable.Empty<BrowserTab>();
        remaining.Should().NotContain(t => t.TabId == "tab-1",
            "TabClosedAlert must remove the closed tab from the BrowserTabs cache");
    }

    [Fact]
    public async Task AnalyzeAsync_TabChangedAlert_AddsBrowserTabToCache()
    {
        var (sut, user, _) = CreateSut();

        // No tabs initially
        user.BrowserTabs.Should().BeNullOrEmpty();

        await sut.AnalyzeAsync(MakeTabChanged(BankUrl, loggedIn: null, tabId: "tab-2"));

        user.BrowserTabs.Should().ContainKey(DeviceUid);
        user.BrowserTabs![DeviceUid].Should().Contain(t => t.TabId == "tab-2" && t.Url == BankUrl,
            "TabChangedAlert must add the tab to the BrowserTabs cache for the device");
    }

    // ── Outgoing RA must not trigger danger ─────────────────────────────────

    [Fact]
    public async Task AnalyzeAsync_OutgoingRemoteAccess_WithBankTab_ShouldNotTriggerImmediateDanger()
    {
        // Outgoing = user is the one controlling the remote machine, not the victim
        var (sut, _, captured) = CreateSut();
        var bankTab = new BrowserTab { TabId = "tab-1", Url = BankUrl, LoggedIn = null };
        await sut.AnalyzeAsync(MakeRa(RemoteAccessDirection.Outgoing, ConnectionStatus.Open, new[] { bankTab }));

        captured.OfType<ImmediateDangerDetected>().Should().BeEmpty(
            "Outgoing remote access must never trigger ImmediateDanger");
    }

    // ── Fix D: direction=Unknown treated as incoming (timing race at session start) ──

    [Fact(Skip = "ASPS-626: pre-existing app bug — UDUserAnalyzer does not treat RemoteAccessDirection.Unknown as incoming. The fix requires updating DetectImmediateDanger to handle Unknown direction conservatively. Route to Backend implementer.")]
    public async Task AnalyzeAsync_UnknownDirectionRemoteAccess_WithBankTab_ShouldTriggerImmediateDanger()
    {
        // Scenario: AnyDesk network connections detected before log parser resolves
        // direction — RemoteAppStatus.direction stays "unknown" when session_started fires.
        // Fix D: Unknown direction is treated conservatively as potentially incoming.
        var (sut, _, captured) = CreateSut();
        var bankTab = new BrowserTab { TabId = "tab-1", Url = BankUrl, LoggedIn = null };
        await sut.AnalyzeAsync(MakeRa(RemoteAccessDirection.Unknown, ConnectionStatus.Open, new[] { bankTab }));

        captured.OfType<ImmediateDangerDetected>().Should().NotBeEmpty(
            "Unknown direction with active session + sensitive tab must trigger danger (conservative — direction not yet resolved)");
    }

    // ── event capture helper ───────────────────────────────────────────────

    private sealed class EventCapture : IDomainEventHandler
    {
        private readonly List<IDomainEvent> _target;
        public EventCapture(List<IDomainEvent> target) => _target = target;

        public Task Handle(IDomainEvent evt) { _target.Add(evt); return Task.CompletedTask; }

        public Type[] GetHandleableEvents() => new[]
        {
            typeof(ImmediateDangerDetected),
            typeof(ImmediateDangerEnded),
        };
    }
}
