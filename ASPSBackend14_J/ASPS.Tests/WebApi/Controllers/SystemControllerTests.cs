using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebApi.Controllers;
using WebApi.Services;
using Business.Commands;
using Common.Messaging;
using System;
using System.Threading.Tasks;

namespace ASPS.Tests.WebApi.Controllers;

/// <summary>
/// Unit tests for SystemController (FR-035 + ASPS-649).
///
/// [Authorize(Roles = "Admin")] is not enforced here — unit tests instantiate
/// the controller directly without an HTTP pipeline, so auth middleware is
/// bypassed.
/// </summary>
public class SystemControllerTests
{
    private readonly Mock<ICQRSClient> _cqrsMock = new();
    private readonly Mock<ILogger<SystemController>> _loggerMock = new();

    private SystemController CreateSut() =>
        new SystemController(_cqrsMock.Object, _loggerMock.Object);

    #region GET /api/system/version

    [Fact]
    public void GetVersion_ReturnsOkResult()
    {
        var result = CreateSut().GetVersion();
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetVersion_ResponseContains_VersionField()
    {
        var result = CreateSut().GetVersion() as OkObjectResult;
        var value = result!.Value;
        var versionProp = value!.GetType().GetProperty("Version");

        versionProp.Should().NotBeNull("response must include a Version property");
        var version = versionProp!.GetValue(value) as string;
        version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetVersion_ResponseContains_BuildDateField()
    {
        var result = CreateSut().GetVersion() as OkObjectResult;
        var value = result!.Value;
        var buildDateProp = value!.GetType().GetProperty("BuildDate");

        buildDateProp.Should().NotBeNull("response must include a BuildDate property");
        buildDateProp!.GetValue(value).Should().BeOfType<DateTime>();
    }

    [Fact]
    public void GetVersion_ResponseContains_GitCommitIdField()
    {
        var result = CreateSut().GetVersion() as OkObjectResult;
        var value = result!.Value;
        var gitCommitIdProp = value!.GetType().GetProperty("GitCommitId");

        gitCommitIdProp.Should().NotBeNull("response must include a GitCommitId property");
    }

    [Fact]
    public void GetVersion_ResponseContains_PrereleaseAndPublicReleaseFlags()
    {
        var result = CreateSut().GetVersion() as OkObjectResult;
        var value = result!.Value;
        var valueType = value!.GetType();

        var isPrereleaseProp    = valueType.GetProperty("IsPrerelease");
        var isPublicReleaseProp = valueType.GetProperty("IsPublicRelease");

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
        var result = CreateSut().Health();
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void Health_ResponseContains_StatusEqualsHealthy()
    {
        var result = CreateSut().Health() as OkObjectResult;
        var value = result!.Value;
        var statusProp = value!.GetType().GetProperty("Status");

        statusProp.Should().NotBeNull("health response must include a Status property");
        var status = statusProp!.GetValue(value) as string;
        status.Should().Be("healthy");
    }

    [Fact]
    public void Health_ResponseContains_TimestampField_IsRecent()
    {
        var before = DateTime.UtcNow;

        var result = CreateSut().Health() as OkObjectResult;
        var value = result!.Value;
        var timestampProp = value!.GetType().GetProperty("Timestamp");

        timestampProp.Should().NotBeNull("health response must include a Timestamp property");
        var timestamp = (DateTime)timestampProp!.GetValue(value)!;
        timestamp.Should().BeOnOrAfter(before);
    }

    #endregion

    #region POST /api/system/reinitialize-asview (ASPS-649)

    [Fact]
    public async Task ReinitializeAsView_WhenSuccess_ReturnsOk()
    {
        _cqrsMock.Setup(c => c.SendCommandAsync<ReInitializeASViewCommandResult>(It.IsAny<Command>()))
            .ReturnsAsync(new ReInitializeASViewCommandResult
            {
                Success = true,
                Message = "ASView re-initialized successfully!"
            });

        var result = await CreateSut().ReinitializeAsView();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ReinitializeAsView_WhenFailure_ReturnsBadRequest()
    {
        _cqrsMock.Setup(c => c.SendCommandAsync<ReInitializeASViewCommandResult>(It.IsAny<Command>()))
            .ReturnsAsync(new ReInitializeASViewCommandResult
            {
                Success = false,
                Message = "Backend unavailable"
            });

        var result = await CreateSut().ReinitializeAsView();

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ReinitializeAsView_WhenException_Returns500()
    {
        _cqrsMock.Setup(c => c.SendCommandAsync<ReInitializeASViewCommandResult>(It.IsAny<Command>()))
            .ThrowsAsync(new Exception("NetMQ timeout"));

        var result = await CreateSut().ReinitializeAsView();

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
    }

    #endregion
}
