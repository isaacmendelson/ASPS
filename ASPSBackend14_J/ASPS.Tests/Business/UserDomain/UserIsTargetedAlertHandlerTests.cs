using Xunit;
using FluentAssertions;
using Business.RealtimeAnalysis.UserDomain;
using Common.Models;

namespace ASPS.Tests.Business.UserDomain;

/// <summary>
/// Unit tests for UserIsTargetedAlertHandler (ASPS-370)
/// </summary>
public class UserIsTargetedAlertHandlerTests
{
    #region Test Helpers

    private UDUser CreateTestUser(bool isTargeted = false)
    {
        var key = new Key("User", Guid.NewGuid().ToString());
        var userInfo = new UserInfo(
            key,
            "keycloak-123",
            "Test",
            "User",
            "Address",
            "City",
            "State",
            "12345",
            "IL",
            "+972501234567",
            Common.Enums.UserRole.Self,
            false,
            DateTime.UtcNow,
            null,
            "en-US",
            0
        );
        var riskAssessment = new RiskAssessment(0, "", false, 0.5f);

        return new UDUser(key, userInfo, riskAssessment, null, null, null, isTargeted);
    }

    #endregion

    #region CheckAndCreateAlert Tests

    [Fact]
    public void CheckAndCreateAlert_WithNonTargetedUser_CreatesAlert()
    {
        // Arrange
        var sut = new UserIsTargetedAlertHandler();
        var user = CreateTestUser(isTargeted: false);
        var lists = new List<string> { "Darknet List A", "Scam Database B" };

        // Act
        var result = sut.CheckAndCreateAlert(
            user: user,
            userEmail: "victim@example.com",
            userPhoneNumber: "+972501234567",
            foundInLists: lists,
            source: "Darknet monitoring",
            confidenceScore: 0.85
        );

        // Assert
        result.Should().NotBeNull();
        result!.EventType.Should().Be("UserIsTargetedAlertReceived");
        result.UserKeyField.Should().Be(user.Key.Value);
        result.UserEmail.Should().Be("victim@example.com");
        result.UserPhoneNumber.Should().Be("+972501234567");
        result.FoundInLists.Should().BeEquivalentTo(lists);
        result.Source.Should().Be("Darknet monitoring");
        result.CorrelationConfidence.Should().Be(0.85);
    }

    [Fact]
    public void CheckAndCreateAlert_WithAlreadyTargetedUser_ReturnsNull()
    {
        // Arrange
        var sut = new UserIsTargetedAlertHandler();
        var user = CreateTestUser(isTargeted: true);
        var lists = new List<string> { "List A" };

        // Act
        var result = sut.CheckAndCreateAlert(
            user: user,
            userEmail: "victim@example.com",
            userPhoneNumber: "+972501234567",
            foundInLists: lists,
            source: "Test",
            confidenceScore: 0.9
        );

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CheckAndCreateAlert_SetsUserIsTargetedFlag()
    {
        // Arrange
        var sut = new UserIsTargetedAlertHandler();
        var user = CreateTestUser(isTargeted: false);

        // Act
        var result = sut.CheckAndCreateAlert(
            user: user,
            userEmail: "victim@example.com",
            userPhoneNumber: "+972501234567",
            foundInLists: new List<string> { "List A" },
            source: "Test",
            confidenceScore: 0.7
        );

        // Assert
        result.Should().NotBeNull();
        user.IsTargeted.Should().BeTrue();
    }

    [Fact]
    public void CheckAndCreateAlert_ClampsConfidenceScore()
    {
        // Arrange
        var sut = new UserIsTargetedAlertHandler();
        var user = CreateTestUser(isTargeted: false);

        // Act - Test upper clamp
        var result1 = sut.CheckAndCreateAlert(
            user: user,
            userEmail: "test@example.com",
            userPhoneNumber: "+972501234567",
            foundInLists: new List<string> { "List A" },
            source: "Test",
            confidenceScore: 1.5
        );

        // Assert
        result1!.CorrelationConfidence.Should().Be(1.0);

        // Arrange - Create new user for lower clamp test
        var user2 = CreateTestUser(isTargeted: false);

        // Act - Test lower clamp
        var result2 = sut.CheckAndCreateAlert(
            user: user2,
            userEmail: "test@example.com",
            userPhoneNumber: "+972501234567",
            foundInLists: new List<string> { "List A" },
            source: "Test",
            confidenceScore: -0.5
        );

        // Assert
        result2!.CorrelationConfidence.Should().Be(0.0);
    }

    #endregion

    #region FindUserInLeadLists Tests

    [Fact]
    public void FindUserInLeadLists_WithEmailMatch_ReturnsMatchingLists()
    {
        // Arrange
        var sut = new UserIsTargetedAlertHandler();
        var leadLists = new Dictionary<string, HashSet<string>>
        {
            { "List A", new HashSet<string> { "victim@example.com", "other@example.com" } },
            { "List B", new HashSet<string> { "someone@example.com" } }
        };

        // Act
        var result = sut.FindUserInLeadLists(
            email: "victim@example.com",
            phoneNumber: "",
            knownLeadLists: leadLists
        );

        // Assert
        result.Should().Contain("List A");
        result.Should().NotContain("List B");
    }

    [Fact]
    public void FindUserInLeadLists_WithPhoneMatch_ReturnsMatchingLists()
    {
        // Arrange
        var sut = new UserIsTargetedAlertHandler();
        var leadLists = new Dictionary<string, HashSet<string>>
        {
            { "List A", new HashSet<string> { "0501234567" } },
            { "List B", new HashSet<string> { "0509876543" } }
        };

        // Act
        var result = sut.FindUserInLeadLists(
            email: "",
            phoneNumber: "050-123-4567", // With separators
            knownLeadLists: leadLists
        );

        // Assert
        result.Should().Contain("List A");
        result.Should().NotContain("List B");
    }

    [Fact]
    public void FindUserInLeadLists_WithMultipleMatches_ReturnsAllMatchingLists()
    {
        // Arrange
        var sut = new UserIsTargetedAlertHandler();
        var leadLists = new Dictionary<string, HashSet<string>>
        {
            { "List A", new HashSet<string> { "victim@example.com" } },
            { "List B", new HashSet<string> { "victim@example.com" } },
            { "List C", new HashSet<string> { "other@example.com" } }
        };

        // Act
        var result = sut.FindUserInLeadLists(
            email: "victim@example.com",
            phoneNumber: "",
            knownLeadLists: leadLists
        );

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("List A");
        result.Should().Contain("List B");
        result.Should().NotContain("List C");
    }

    [Fact]
    public void FindUserInLeadLists_WithNoMatches_ReturnsEmpty()
    {
        // Arrange
        var sut = new UserIsTargetedAlertHandler();
        var leadLists = new Dictionary<string, HashSet<string>>
        {
            { "List A", new HashSet<string> { "other@example.com" } }
        };

        // Act
        var result = sut.FindUserInLeadLists(
            email: "victim@example.com",
            phoneNumber: "",
            knownLeadLists: leadLists
        );

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void FindUserInLeadLists_IsCaseInsensitive()
    {
        // Arrange
        var sut = new UserIsTargetedAlertHandler();
        var leadLists = new Dictionary<string, HashSet<string>>
        {
            { "List A", new HashSet<string> { "victim@example.com" } }
        };

        // Act
        var result = sut.FindUserInLeadLists(
            email: "VICTIM@EXAMPLE.COM",
            phoneNumber: "",
            knownLeadLists: leadLists
        );

        // Assert
        result.Should().Contain("List A");
    }

    #endregion

    #region CalculateConfidenceScore Tests

    [Fact]
    public void CalculateConfidenceScore_WithOneListEmailPhone_Returns70Percent()
    {
        // Arrange
        var sut = new UserIsTargetedAlertHandler();

        // Act - 1 list (0.2) + email (0.25) + phone (0.25) = 0.7
        var result = sut.CalculateConfidenceScore(
            foundInListsCount: 1,
            hasEmail: true,
            hasPhone: true
        );

        // Assert
        result.Should().Be(0.7);
    }

    [Fact]
    public void CalculateConfidenceScore_WithTwoListsEmailPhone_Returns80Percent()
    {
        // Arrange
        var sut = new UserIsTargetedAlertHandler();

        // Act - 2 lists (0.3) + email (0.25) + phone (0.25) = 0.8
        var result = sut.CalculateConfidenceScore(
            foundInListsCount: 2,
            hasEmail: true,
            hasPhone: true
        );

        // Assert
        result.Should().Be(0.8);
    }

    [Fact]
    public void CalculateConfidenceScore_WithThreeOrMoreLists_Returns100Percent()
    {
        // Arrange
        var sut = new UserIsTargetedAlertHandler();

        // Act - 3+ lists (0.5) + email (0.25) + phone (0.25) = 1.0
        var result = sut.CalculateConfidenceScore(
            foundInListsCount: 3,
            hasEmail: true,
            hasPhone: true
        );

        // Assert
        result.Should().Be(1.0);
    }

    [Fact]
    public void CalculateConfidenceScore_WithOnlyEmail_Returns45Percent()
    {
        // Arrange
        var sut = new UserIsTargetedAlertHandler();

        // Act - 1 list (0.2) + email (0.25) + no phone = 0.45
        var result = sut.CalculateConfidenceScore(
            foundInListsCount: 1,
            hasEmail: true,
            hasPhone: false
        );

        // Assert
        result.Should().Be(0.45);
    }

    [Fact]
    public void CalculateConfidenceScore_WithOnlyPhone_Returns45Percent()
    {
        // Arrange
        var sut = new UserIsTargetedAlertHandler();

        // Act - 1 list (0.2) + no email + phone (0.25) = 0.45
        var result = sut.CalculateConfidenceScore(
            foundInListsCount: 1,
            hasEmail: false,
            hasPhone: true
        );

        // Assert
        result.Should().Be(0.45);
    }

    [Fact]
    public void CalculateConfidenceScore_WithNoLists_Returns0()
    {
        // Arrange
        var sut = new UserIsTargetedAlertHandler();

        // Act
        var result = sut.CalculateConfidenceScore(
            foundInListsCount: 0,
            hasEmail: false,
            hasPhone: false
        );

        // Assert
        result.Should().Be(0.0);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void UserIsTargetedAlert_FullWorkflow_ElderlyVictimScenario()
    {
        // Arrange
        var sut = new UserIsTargetedAlertHandler();
        var user = CreateTestUser(isTargeted: false);
        
        var leadLists = new Dictionary<string, HashSet<string>>
        {
            { "Darknet Elderly Victims 2025", new HashSet<string> { "elderly@example.com" } },
            { "Banking Scam Targets IL", new HashSet<string> { "0501234567" } },
            { "Phishing Campaign Q1", new HashSet<string> { "elderly@example.com", "0501234567" } }
        };

        // Act - Find user in lists
        var matchingLists = sut.FindUserInLeadLists(
            email: "elderly@example.com",
            phoneNumber: "+972-50-123-4567",
            knownLeadLists: leadLists
        );

        // Calculate confidence
        var confidence = sut.CalculateConfidenceScore(
            foundInListsCount: matchingLists.Count,
            hasEmail: true,
            hasPhone: true
        );

        // Create alert
        var alert = sut.CheckAndCreateAlert(
            user: user,
            userEmail: "elderly@example.com",
            userPhoneNumber: "+972501234567",
            foundInLists: matchingLists,
            source: "Darknet monitoring system",
            confidenceScore: confidence
        );

        // Assert
        alert.Should().NotBeNull();
        alert!.EventType.Should().Be("UserIsTargetedAlertReceived");
        alert.FoundInLists.Should().HaveCount(3);
        alert.CorrelationConfidence.Should().Be(1.0); // 3+ lists + email + phone
        user.IsTargeted.Should().BeTrue();

        // Act - Try to create alert again (should fail - already targeted)
        var secondAlert = sut.CheckAndCreateAlert(
            user: user,
            userEmail: "elderly@example.com",
            userPhoneNumber: "+972501234567",
            foundInLists: matchingLists,
            source: "Darknet monitoring system",
            confidenceScore: confidence
        );

        // Assert - No second alert
        secondAlert.Should().BeNull();
    }

    #endregion
}
