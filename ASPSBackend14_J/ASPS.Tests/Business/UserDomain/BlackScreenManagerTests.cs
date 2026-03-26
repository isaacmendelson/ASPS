using Xunit;
using FluentAssertions;
using Business.RealtimeAnalysis.UserDomain;

namespace ASPS.Tests.Business.UserDomain;

/// <summary>
/// Unit tests for BlackScreenManager (ASPS-369)
/// </summary>
public class BlackScreenManagerTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_InitializesDefaultSelectors()
    {
        // Act
        var sut = new BlackScreenManager();

        // Assert
        sut.Should().NotBeNull();
        var defaults = sut.GetDefaultSelectors();
        defaults.Should().NotBeEmpty();
        defaults.Should().Contain(".account-balance");
        defaults.Should().Contain(".card-number");
        defaults.Should().Contain("input[type='password']");
    }

    #endregion

    #region ActivateBlackScreen Tests

    [Fact]
    public void ActivateBlackScreen_WithMinimalParams_ReturnsEvent()
    {
        // Arrange
        var sut = new BlackScreenManager();

        // Act
        var result = sut.ActivateBlackScreen(
            userKeyField: "user-123",
            deviceUid: "device-001",
            targetUrl: "https://bank.com/account",
            remoteAccessApp: "TeamViewer",
            reason: "High risk scam detected"
        );

        // Assert
        result.Should().NotBeNull();
        result.EventType.Should().Be("BlackScreenActivated");
        result.UserKeyField.Should().Be("user-123");
        result.DeviceUid.Should().Be("device-001");
        result.TargetUrl.Should().Be("https://bank.com/account");
        result.RemoteAccessApp.Should().Be("TeamViewer");
        result.Reason.Should().Be("High risk scam detected");
    }

    [Fact]
    public void ActivateBlackScreen_IncludesDefaultSelectors()
    {
        // Arrange
        var sut = new BlackScreenManager();

        // Act
        var result = sut.ActivateBlackScreen(
            userKeyField: "user-123",
            deviceUid: "device-001",
            targetUrl: "https://bank.com/account",
            remoteAccessApp: "AnyDesk",
            reason: "Remote access detected"
        );

        // Assert
        result.HiddenElements.Should().NotBeEmpty();
        result.HiddenElements.Should().Contain(".account-balance");
        result.HiddenElements.Should().Contain(".card-number");
        result.HiddenElements.Should().Contain("input[type='password']");
    }

    [Fact]
    public void ActivateBlackScreen_WithCustomSelectors_CombinesBoth()
    {
        // Arrange
        var sut = new BlackScreenManager();
        var customSelectors = new List<string> { ".custom-sensitive", "#special-field" };

        // Act
        var result = sut.ActivateBlackScreen(
            userKeyField: "user-123",
            deviceUid: "device-001",
            targetUrl: "https://bank.com/account",
            remoteAccessApp: "TeamViewer",
            reason: "Scam protection",
            customSelectors: customSelectors
        );

        // Assert
        result.HiddenElements.Should().Contain(".account-balance"); // Default
        result.HiddenElements.Should().Contain(".custom-sensitive"); // Custom
        result.HiddenElements.Should().Contain("#special-field"); // Custom
    }

    [Fact]
    public void ActivateBlackScreen_GeneratesScript()
    {
        // Arrange
        var sut = new BlackScreenManager();

        // Act
        var result = sut.ActivateBlackScreen(
            userKeyField: "user-123",
            deviceUid: "device-001",
            targetUrl: "https://bank.com/account",
            remoteAccessApp: "TeamViewer",
            reason: "Protection"
        );

        // Assert
        result.InjectedScript.Should().NotBeNullOrEmpty();
        result.InjectedScript.Should().Contain("ASPS Black Screen Protection");
        result.InjectedScript.Should().Contain("querySelectorAll");
    }

    [Fact]
    public void ActivateBlackScreen_SetsActivationTimestamp()
    {
        // Arrange
        var sut = new BlackScreenManager();
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var result = sut.ActivateBlackScreen(
            userKeyField: "user-123",
            deviceUid: "device-001",
            targetUrl: "https://bank.com/account",
            remoteAccessApp: "TeamViewer",
            reason: "Protection"
        );

        // Assert
        var after = DateTime.UtcNow.AddSeconds(1);
        result.ActivationTimestamp.Should().BeAfter(before);
        result.ActivationTimestamp.Should().BeBefore(after);
    }

    #endregion

    #region GenerateHidingScript Tests

    [Fact]
    public void GenerateHidingScript_WithSelectors_GeneratesValidScript()
    {
        // Arrange
        var sut = new BlackScreenManager();
        var selectors = new List<string> { ".balance", "#cardNumber" };

        // Act
        var script = sut.GenerateHidingScript(selectors);

        // Assert
        script.Should().NotBeNullOrEmpty();
        script.Should().Contain("'.balance'");
        script.Should().Contain("'#cardNumber'");
    }

    [Fact]
    public void GenerateHidingScript_IncludesBlackScreenCSS()
    {
        // Arrange
        var sut = new BlackScreenManager();
        var selectors = new List<string> { ".test" };

        // Act
        var script = sut.GenerateHidingScript(selectors);

        // Assert
        script.Should().Contain("background: black !important");
        script.Should().Contain("color: transparent !important");
        script.Should().Contain("asps-black-screen-hidden");
    }

    [Fact]
    public void GenerateHidingScript_IncludesMutationObserver()
    {
        // Arrange
        var sut = new BlackScreenManager();
        var selectors = new List<string> { ".dynamic" };

        // Act
        var script = sut.GenerateHidingScript(selectors);

        // Assert
        script.Should().Contain("MutationObserver");
        script.Should().Contain("childList: true");
        script.Should().Contain("subtree: true");
    }

    [Fact]
    public void GenerateHidingScript_EscapesQuotesInSelectors()
    {
        // Arrange
        var sut = new BlackScreenManager();
        var selectors = new List<string> { "input[name='test']" };

        // Act
        var script = sut.GenerateHidingScript(selectors);

        // Assert
        script.Should().Contain("\\'"); // Escaped quote
    }

    [Fact]
    public void GenerateHidingScript_WithEmptySelectors_GeneratesValidScript()
    {
        // Arrange
        var sut = new BlackScreenManager();
        var selectors = new List<string>();

        // Act
        var script = sut.GenerateHidingScript(selectors);

        // Assert
        script.Should().NotBeNullOrEmpty();
        script.Should().Contain("ASPS Black Screen Protection");
    }

    #endregion

    #region Selector Management Tests

    [Fact]
    public void GetDefaultSelectors_ReturnsAllDefaults()
    {
        // Arrange
        var sut = new BlackScreenManager();

        // Act
        var selectors = sut.GetDefaultSelectors();

        // Assert
        selectors.Should().NotBeEmpty();
        selectors.Should().Contain(s => s.Contains("balance"));
        selectors.Should().Contain(s => s.Contains("card"));
        selectors.Should().Contain(s => s.Contains("password"));
    }

    [Fact]
    public void AddDefaultSelector_AddsNewSelector()
    {
        // Arrange
        var sut = new BlackScreenManager();
        var newSelector = ".custom-sensitive-field";

        // Act
        sut.AddDefaultSelector(newSelector);

        // Assert
        var selectors = sut.GetDefaultSelectors();
        selectors.Should().Contain(newSelector);
    }

    [Fact]
    public void AddDefaultSelector_WithDuplicate_DoesNotAddTwice()
    {
        // Arrange
        var sut = new BlackScreenManager();
        var selector = ".test-field";

        // Act
        sut.AddDefaultSelector(selector);
        sut.AddDefaultSelector(selector); // Add again

        // Assert
        var selectors = sut.GetDefaultSelectors();
        selectors.Count(s => s == selector).Should().Be(1);
    }

    [Fact]
    public void RemoveDefaultSelector_RemovesSelector()
    {
        // Arrange
        var sut = new BlackScreenManager();
        var selectorToRemove = ".account-balance";

        // Act
        sut.RemoveDefaultSelector(selectorToRemove);

        // Assert
        var selectors = sut.GetDefaultSelectors();
        selectors.Should().NotContain(selectorToRemove);
    }

    [Fact]
    public void RemoveDefaultSelector_WithNonExistent_DoesNotThrow()
    {
        // Arrange
        var sut = new BlackScreenManager();

        // Act
        Action act = () => sut.RemoveDefaultSelector(".non-existent");

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void BlackScreenManager_FullWorkflow_BankingScenario()
    {
        // Arrange
        var sut = new BlackScreenManager();
        sut.AddDefaultSelector(".transaction-history");
        
        var customSelectors = new List<string> 
        { 
            ".secret-code",
            "#tfa-input" 
        };

        // Act
        var result = sut.ActivateBlackScreen(
            userKeyField: "user-elderly-victim",
            deviceUid: "laptop-001",
            targetUrl: "https://bank-hapoalim.co.il/account",
            remoteAccessApp: "TeamViewer",
            reason: "Active scam detected - OTP interception + remote access",
            customSelectors: customSelectors
        );

        // Assert
        result.Should().NotBeNull();
        result.EventType.Should().Be("BlackScreenActivated");
        result.UserKeyField.Should().Be("user-elderly-victim");
        result.RemoteAccessApp.Should().Be("TeamViewer");
        result.Reason.Should().Contain("scam detected");
        
        // Verify all selectors are included
        result.HiddenElements.Should().Contain(".account-balance"); // Default
        result.HiddenElements.Should().Contain(".transaction-history"); // Added default
        result.HiddenElements.Should().Contain(".secret-code"); // Custom
        result.HiddenElements.Should().Contain("#tfa-input"); // Custom
        
        // Verify script generation
        result.InjectedScript.Should().Contain("ASPS Black Screen Protection");
        result.InjectedScript.Should().Contain(".secret-code");
        result.InjectedScript.Should().Contain("MutationObserver");
    }

    [Fact]
    public void GenerateHidingScript_ContainsAllRequiredElements()
    {
        // Arrange
        var sut = new BlackScreenManager();
        var selectors = new List<string> { ".test1", ".test2", "#test3" };

        // Act
        var script = sut.GenerateHidingScript(selectors);

        // Assert - Check all critical parts
        script.Should().Contain("const ASPS_HIDE_CLASS");
        script.Should().Contain("createElement('style')");
        script.Should().Contain("background: black !important");
        script.Should().Contain("color: transparent !important");
        script.Should().Contain("querySelectorAll");
        script.Should().Contain("classList.add");
        script.Should().Contain("new MutationObserver");
        script.Should().Contain("observe(document.body");
        script.Should().Contain("'.test1'");
        script.Should().Contain("'.test2'");
        script.Should().Contain("'#test3'");
    }

    #endregion
}
