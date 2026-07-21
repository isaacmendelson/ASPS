using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using WebApi.Controllers;
using System;

namespace ASPS.Tests.WebApi.Controllers;

/// <summary>
/// Unit tests for SystemController (FR-035).
///
/// [Authorize(Roles = "Admin")] is not enforced here — unit tests instantiate
/// the controller directly without an HTTP pipeline, so auth middleware is
/// bypassed. This matches the approach used by VersionControllerTests.
/// </summary>
public class SystemControllerTests
{
    private readonly SystemController _sut;

    public SystemControllerTests()
    {
        _sut = new SystemController();
    }

    #region GET /api/system/version

    [Fact]
    public void GetVersion_ReturnsOkResult()
    {
        // Act
        var result = _sut.GetVersion();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetVersion_ResponseContains_VersionField()
    {
        // Act
        var result = _sut.GetVersion() as OkObjectResult;
        var value = result!.Value;
        var versionProp = value!.GetType().GetProperty("Version");

        // Assert
        versionProp.Should().NotBeNull("response must include a Version property");
        var version = versionProp!.GetValue(value) as string;
        version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetVersion_ResponseContains_BuildDateField()
    {
        // Act
        var result = _sut.GetVersion() as OkObjectResult;
        var value = result!.Value;
        var buildDateProp = value!.GetType().GetProperty("BuildDate");

        // Assert
        buildDateProp.Should().NotBeNull("response must include a BuildDate property");
        buildDateProp!.GetValue(value).Should().BeOfType<DateTime>();
    }

    [Fact]
    public void GetVersion_ResponseContains_GitCommitIdField()
    {
        // Act
        var result = _sut.GetVersion() as OkObjectResult;
        var value = result!.Value;
        var gitCommitIdProp = value!.GetType().GetProperty("GitCommitId");

        // Assert
        gitCommitIdProp.Should().NotBeNull("response must include a GitCommitId property");
    }

    [Fact]
    public void GetVersion_ResponseContains_PrereleaseAndPublicReleaseFlags()
    {
        // Act
        var result = _sut.GetVersion() as OkObjectResult;
        var value = result!.Value;
        var valueType = value!.GetType();

        var isPrereleaseProp   = valueType.GetProperty("IsPrerelease");
        var isPublicReleaseProp = valueType.GetProperty("IsPublicRelease");

        // Assert
        isPrereleaseProp.Should().NotBeNull("response must include IsPrerelease");
        isPublicReleaseProp.Should().NotBeNull("response must include IsPublicRelease");
        isPrereleaseProp!.GetValue(value).Should().BeOfType<bool>();
        isPublicReleaseProp!.GetValue(value).Should().BeOfType<bool>();
    }

    #endregion

    #region GET /api/system/health

    [Fact]
    public void Health_ReturnsOkResult()
    {
        // Act
        var result = _sut.Health();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void Health_ResponseContains_StatusEqualsHealthy()
    {
        // Act
        var result = _sut.Health() as OkObjectResult;
        var value = result!.Value;
        var statusProp = value!.GetType().GetProperty("Status");

        // Assert
        statusProp.Should().NotBeNull("health response must include a Status property");
        var status = statusProp!.GetValue(value) as string;
        status.Should().Be("healthy");
    }

    [Fact]
    public void Health_ResponseContains_TimestampField_IsRecent()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var result = _sut.Health() as OkObjectResult;
        var value = result!.Value;
        var timestampProp = value!.GetType().GetProperty("Timestamp");

        // Assert
        timestampProp.Should().NotBeNull("health response must include a Timestamp property");
        var timestamp = (DateTime)timestampProp!.GetValue(value)!;
        timestamp.Should().BeOnOrAfter(before);
    }

    #endregion
}
