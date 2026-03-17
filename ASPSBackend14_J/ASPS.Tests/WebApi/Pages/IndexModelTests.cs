using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using WebApi.Pages;
using WebApi.Services;

namespace ASPS.Tests.WebApi.Pages;

public class IndexModelTests
{
    private readonly IndexModel _sut;

    public IndexModelTests()
    {
        var cqrsLoggerMock = new Mock<ILogger<CQRSClient>>();
        var cqrsClient = new CQRSClient(
            "tcp://localhost:5000", 
            cqrsLoggerMock.Object, 
            null);
        var loggerMock = new Mock<ILogger<IndexModel>>();
        _sut = new IndexModel(cqrsClient, loggerMock.Object);
    }

    [Fact]
    public void Constructor_InitializesVersionProperties()
    {
        // Assert
        _sut.BackendVersion.Should().Be("N/A");
        _sut.WebApiVersion.Should().NotBeNullOrEmpty();
        _sut.AdminVersion.Should().NotBeNullOrEmpty();
        _sut.WebApiVersion.Should().Be(_sut.AdminVersion, "Admin version should equal WebApi version");
    }

    [Fact]
    public void VersionProperties_FollowSemanticVersioning()
    {
        // Assert
        _sut.WebApiVersion.Should().Contain(".");
        _sut.AdminVersion.Should().Contain(".");
    }

    [Fact]
    public void SystemVersion_MatchesWebApiVersion()
    {
        // Assert
        _sut.SystemVersion.Should().Be(_sut.WebApiVersion);
    }
}
