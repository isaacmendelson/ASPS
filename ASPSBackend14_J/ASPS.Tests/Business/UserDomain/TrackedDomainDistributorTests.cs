using Xunit;
using FluentAssertions;
using Business.RealtimeAnalysis.UserDomain;

namespace ASPS.Tests.Business.UserDomain;

/// <summary>
/// Unit tests for TrackedDomainDistributor (ASPS-371)
/// </summary>
public class TrackedDomainDistributorTests
{
    #region CreateTrackingEvent Tests

    [Fact]
    public void CreateTrackingEvent_WithValidParams_CreatesEvent()
    {
        // Arrange
        var sut = new TrackedDomainDistributor();
        var domains = new List<TrackedDomainInfo>
        {
            new TrackedDomainInfo("phishing.com", "scam-001", TrackMode.Block, ReportType.All, "Phishing")
        };

        // Act
        var result = sut.CreateTrackingEvent(
            userKeyField: "user-123",
            domains: domains,
            isCrossPlatformLock: true,
            reason: "Active scam detected"
        );

        // Assert
        result.Should().NotBeNull();
        result.EventType.Should().Be("SetTrackedDomains");
        result.UserKeyField.Should().Be("user-123");
        result.TrackedDomains.Should().HaveCount(1);
        result.IsCrossPlatformLock.Should().BeTrue();
        result.Reason.Should().Be("Active scam detected");
    }

    #endregion

    #region CreateTrackedDomain Tests

    [Fact]
    public void CreateTrackedDomain_WithFullUrl_ExtractsDomain()
    {
        // Arrange
        var sut = new TrackedDomainDistributor();

        // Act
        var result = sut.CreateTrackedDomain(
            url: "https://phishing-site.com/login",
            scamKey: "scam-001",
            trackMode: TrackMode.Block,
            reportType: ReportType.All,
            reason: "Phishing attempt"
        );

        // Assert
        result.Should().NotBeNull();
        result.Domain.Should().Be("phishing-site.com");
        result.ScamInProgressKey.Should().Be("scam-001");
        result.TrackMode.Should().Be(TrackMode.Block);
        result.ReportType.Should().Be(ReportType.All);
        result.Reason.Should().Be("Phishing attempt");
    }

    #endregion

    #region ExtractDomain Tests

    [Theory]
    [InlineData("https://example.com", "example.com")]
    [InlineData("http://example.com", "example.com")]
    [InlineData("https://example.com/path", "example.com")]
    [InlineData("https://example.com:8080", "example.com")]
    [InlineData("https://example.com:8080/path", "example.com")]
    [InlineData("example.com", "example.com")]
    [InlineData("EXAMPLE.COM", "example.com")]
    public void ExtractDomain_WithVariousUrls_ExtractsCorrectly(string input, string expected)
    {
        // Arrange
        var sut = new TrackedDomainDistributor();

        // Act
        var result = sut.ExtractDomain(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ExtractDomain_WithEmptyString_ReturnsEmpty()
    {
        // Arrange
        var sut = new TrackedDomainDistributor();

        // Act
        var result = sut.ExtractDomain("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractDomain_WithSubdomain_PreservesSubdomain()
    {
        // Arrange
        var sut = new TrackedDomainDistributor();

        // Act
        var result = sut.ExtractDomain("https://login.phishing.com/auth");

        // Assert
        result.Should().Be("login.phishing.com");
    }

    #endregion

    #region DetermineTrackMode Tests

    [Theory]
    [InlineData(0, TrackMode.Monitor)]
    [InlineData(20, TrackMode.Monitor)]
    [InlineData(39, TrackMode.Monitor)]
    [InlineData(40, TrackMode.Warn)]
    [InlineData(50, TrackMode.Warn)]
    [InlineData(59, TrackMode.Warn)]
    [InlineData(60, TrackMode.HighAlert)]
    [InlineData(70, TrackMode.HighAlert)]
    [InlineData(79, TrackMode.HighAlert)]
    [InlineData(80, TrackMode.Block)]
    [InlineData(90, TrackMode.Block)]
    [InlineData(100, TrackMode.Block)]
    public void DetermineTrackMode_WithVariousRiskScores_ReturnsCorrectMode(double riskScore, TrackMode expected)
    {
        // Arrange
        var sut = new TrackedDomainDistributor();

        // Act
        var result = sut.DetermineTrackMode(riskScore);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region DetermineReportType Tests

    [Theory]
    [InlineData(0, false, ReportType.None)]
    [InlineData(20, false, ReportType.None)]
    [InlineData(39, false, ReportType.None)]
    [InlineData(40, false, ReportType.Backend)]
    [InlineData(50, false, ReportType.Backend)]
    [InlineData(59, false, ReportType.Backend)]
    [InlineData(60, false, ReportType.User)]
    [InlineData(70, false, ReportType.User)]
    [InlineData(79, false, ReportType.User)]
    [InlineData(80, false, ReportType.All)]
    [InlineData(90, false, ReportType.All)]
    [InlineData(100, false, ReportType.All)]
    public void DetermineReportType_WithVariousRiskScores_ReturnsCorrectType(
        double riskScore, 
        bool isTargeted, 
        ReportType expected)
    {
        // Arrange
        var sut = new TrackedDomainDistributor();

        // Act
        var result = sut.DetermineReportType(riskScore, isTargeted);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(10, true, ReportType.All)]
    [InlineData(30, true, ReportType.All)]
    [InlineData(50, true, ReportType.All)]
    [InlineData(70, true, ReportType.All)]
    public void DetermineReportType_WithTargetedUser_AlwaysReturnsAll(
        double riskScore, 
        bool isTargeted, 
        ReportType expected)
    {
        // Arrange
        var sut = new TrackedDomainDistributor();

        // Act
        var result = sut.DetermineReportType(riskScore, isTargeted);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region MergeDomainLists Tests

    [Fact]
    public void MergeDomainLists_WithEmptyLists_ReturnsEmpty()
    {
        // Arrange
        var sut = new TrackedDomainDistributor();

        // Act
        var result = sut.MergeDomainLists(new List<TrackedDomainInfo>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void MergeDomainLists_WithSingleList_ReturnsAll()
    {
        // Arrange
        var sut = new TrackedDomainDistributor();
        var list1 = new List<TrackedDomainInfo>
        {
            new TrackedDomainInfo("domain1.com", "scam-1", TrackMode.Warn, ReportType.User, "Test"),
            new TrackedDomainInfo("domain2.com", "scam-2", TrackMode.Block, ReportType.All, "Test")
        };

        // Act
        var result = sut.MergeDomainLists(list1);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(d => d.Domain == "domain1.com");
        result.Should().Contain(d => d.Domain == "domain2.com");
    }

    [Fact]
    public void MergeDomainLists_WithDuplicates_RemovesDuplicates()
    {
        // Arrange
        var sut = new TrackedDomainDistributor();
        var list1 = new List<TrackedDomainInfo>
        {
            new TrackedDomainInfo("duplicate.com", "scam-1", TrackMode.Warn, ReportType.User, "Test")
        };
        var list2 = new List<TrackedDomainInfo>
        {
            new TrackedDomainInfo("duplicate.com", "scam-2", TrackMode.Monitor, ReportType.Backend, "Test")
        };

        // Act
        var result = sut.MergeDomainLists(list1, list2);

        // Assert
        result.Should().HaveCount(1);
        result[0].Domain.Should().Be("duplicate.com");
    }

    [Fact]
    public void MergeDomainLists_WithDuplicates_KeepsHigherTrackMode()
    {
        // Arrange
        var sut = new TrackedDomainDistributor();
        var list1 = new List<TrackedDomainInfo>
        {
            new TrackedDomainInfo("test.com", "scam-1", TrackMode.Warn, ReportType.User, "Test1")
        };
        var list2 = new List<TrackedDomainInfo>
        {
            new TrackedDomainInfo("test.com", "scam-2", TrackMode.Block, ReportType.All, "Test2")
        };

        // Act
        var result = sut.MergeDomainLists(list1, list2);

        // Assert
        result.Should().HaveCount(1);
        result[0].TrackMode.Should().Be(TrackMode.Block); // Higher mode
    }

    [Fact]
    public void MergeDomainLists_WithMultipleLists_MergesAll()
    {
        // Arrange
        var sut = new TrackedDomainDistributor();
        var list1 = new List<TrackedDomainInfo>
        {
            new TrackedDomainInfo("domain1.com", "scam-1", TrackMode.Warn, ReportType.User, "Test")
        };
        var list2 = new List<TrackedDomainInfo>
        {
            new TrackedDomainInfo("domain2.com", "scam-2", TrackMode.Block, ReportType.All, "Test")
        };
        var list3 = new List<TrackedDomainInfo>
        {
            new TrackedDomainInfo("domain3.com", "scam-3", TrackMode.Monitor, ReportType.Backend, "Test")
        };

        // Act
        var result = sut.MergeDomainLists(list1, list2, list3);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(d => d.Domain == "domain1.com");
        result.Should().Contain(d => d.Domain == "domain2.com");
        result.Should().Contain(d => d.Domain == "domain3.com");
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void TrackedDomainDistributor_FullWorkflow_BankingScam()
    {
        // Arrange
        var sut = new TrackedDomainDistributor();
        var riskScore = 85.0;
        var isTargeted = true;

        // Act - Create tracked domain from suspicious URL
        var domain1 = sut.CreateTrackedDomain(
            url: "https://fake-bank-hapoalim.co.il/login",
            scamKey: "scam-banking-001",
            trackMode: sut.DetermineTrackMode(riskScore),
            reportType: sut.DetermineReportType(riskScore, isTargeted),
            reason: "Phishing impersonating Bank Hapoalim"
        );

        var domain2 = sut.CreateTrackedDomain(
            url: "https://verify-account-secure.com/auth",
            scamKey: "scam-banking-002",
            trackMode: sut.DetermineTrackMode(90),
            reportType: sut.DetermineReportType(90, isTargeted),
            reason: "Generic phishing site"
        );

        // Merge domains
        var allDomains = sut.MergeDomainLists(
            new List<TrackedDomainInfo> { domain1 },
            new List<TrackedDomainInfo> { domain2 }
        );

        // Create distribution event
        var distributionEvent = sut.CreateTrackingEvent(
            userKeyField: "user-elderly-victim-123",
            domains: allDomains,
            isCrossPlatformLock: true,
            reason: "Active banking scam detected - cross-platform protection enabled"
        );

        // Assert
        distributionEvent.Should().NotBeNull();
        distributionEvent.EventType.Should().Be("SetTrackedDomains");
        distributionEvent.UserKeyField.Should().Be("user-elderly-victim-123");
        distributionEvent.IsCrossPlatformLock.Should().BeTrue();
        distributionEvent.TrackedDomains.Should().HaveCount(2);

        // Verify domain 1
        var d1 = distributionEvent.TrackedDomains.First(d => d.Domain == "fake-bank-hapoalim.co.il");
        d1.TrackMode.Should().Be(TrackMode.Block);
        d1.ReportType.Should().Be(ReportType.All);

        // Verify domain 2
        var d2 = distributionEvent.TrackedDomains.First(d => d.Domain == "verify-account-secure.com");
        d2.TrackMode.Should().Be(TrackMode.Block);
        d2.ReportType.Should().Be(ReportType.All);
    }

    #endregion
}
