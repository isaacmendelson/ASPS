using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Business.Queries;
using Common.Entities;
using Common.Enums;
using Common.Models;
using WebApi.Controllers;
using WebApi.Services;

namespace ASPS.Tests.WebApi.Controllers;

/// <summary>
/// Unit tests for DevicesApiController (ASPS-647).
/// </summary>
public class DevicesApiControllerTests
{
    private readonly Mock<ICQRSClient> _cqrsClientMock;
    private readonly Mock<ILogger<DevicesApiController>> _loggerMock;
    private readonly DevicesApiController _controller;

    public DevicesApiControllerTests()
    {
        _cqrsClientMock = new Mock<ICQRSClient>();
        _loggerMock = new Mock<ILogger<DevicesApiController>>();
        _controller = new DevicesApiController(_cqrsClientMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAll_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var pagedResult = new GetAllDevicesPagedQueryResult
        {
            Success = true,
            Result = new PagedResult<DeviceDto>
            {
                Items = new List<DeviceDto> { new DeviceDto { DeviceUid = "DEV-001" } },
                TotalCount = 1,
                Page = 1,
                PageSize = 25
            }
        };

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetAllDevicesPagedQueryResult>(It.IsAny<GetAllDevicesPagedQuery>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAll(new PagedRequest(), status: null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        ok.Value.Should().BeOfType<PagedResult<DeviceDto>>();
        ((PagedResult<DeviceDto>)ok.Value!).TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAll_WhenQueryFails_ReturnsBadRequest()
    {
        // Arrange
        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetAllDevicesPagedQueryResult>(It.IsAny<GetAllDevicesPagedQuery>()))
            .ReturnsAsync(new GetAllDevicesPagedQueryResult { Success = false, Message = "Backend error" });

        // Act
        var result = await _controller.GetAll(new PagedRequest(), status: null);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetAll_WhenExceptionThrown_Returns500()
    {
        // Arrange
        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetAllDevicesPagedQueryResult>(It.IsAny<GetAllDevicesPagedQuery>()))
            .ThrowsAsync(new Exception("Network error"));

        // Act
        var result = await _controller.GetAll(new PagedRequest(), status: null);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        ((ObjectResult)result).StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GetByKey_WhenDeviceFound_ReturnsOk()
    {
        // Arrange
        var device = new PersonalComputer { KeyField = "key-1", DeviceUid = "DEV-001" };

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetDeviceByKeyQueryResult>(It.IsAny<GetDeviceByKeyQuery>()))
            .ReturnsAsync(new GetDeviceByKeyQueryResult { Success = true, Device = device });

        // Act
        var result = await _controller.GetByKey("PersonalComputer", "key-1");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetByKey_WhenDeviceNotFound_ReturnsNotFound()
    {
        // Arrange
        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetDeviceByKeyQueryResult>(It.IsAny<GetDeviceByKeyQuery>()))
            .ReturnsAsync(new GetDeviceByKeyQueryResult { Success = false, Device = null, Message = "Device not found" });

        // Act
        var result = await _controller.GetByKey("PersonalComputer", "missing");

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetByUid_WithValidUid_ReturnsOk()
    {
        // Arrange
        var device = new PersonalComputer { KeyField = "key-1", DeviceUid = "UID-XYZ" };

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetDeviceByUidQueryResult>(It.IsAny<GetDeviceByUidQuery>()))
            .ReturnsAsync(new GetDeviceByUidQueryResult { Success = true, Device = device });

        // Act
        var result = await _controller.GetByUid("UID-XYZ");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetByUid_WithEmptyUid_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetByUid("");

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetAlerts_WithValidKey_ReturnsOk()
    {
        // Arrange
        var pagedResult = new GetDeviceAlertsPagedQueryResult
        {
            Success = true,
            Result = new PagedResult<AlertDto>
            {
                Items = new List<AlertDto>(),
                TotalCount = 0,
                Page = 1,
                PageSize = 25
            }
        };

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetDeviceAlertsPagedQueryResult>(It.IsAny<GetDeviceAlertsPagedQuery>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAlerts("PersonalComputer", "key-1", new PagedRequest());

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }
}
