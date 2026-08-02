using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using WebApi.Controllers;
using WebApi.Services;
using WebApi.DTOs;
using Business.Queries;
using Business.Commands;
using Common.Messaging;
using Common.Models;
using Common.Enums;
using User = global::Common.Entities.User;
using UserWithDeviceCount = global::Business.Queries.UserWithDeviceCount;

namespace ASPS.Tests.WebApi.Controllers;

/// <summary>
/// ASPS-646 — Unit tests for UsersApiController.
/// Verifies paging normalization, response codes, and CQRS delegation.
/// </summary>
public class ASPS646_UsersApiControllerTests
{
    private readonly Mock<ICQRSClient> _cqrsMock = new();
    private readonly Mock<ILogger<UsersApiController>> _loggerMock = new();

    private UsersApiController CreateSut() =>
        new UsersApiController(_cqrsMock.Object, _loggerMock.Object);

    // =========================================================================
    // GET /api/users — paged list
    // =========================================================================

    [Fact]
    public async Task GetAll_ReturnsOkWithPagedResult()
    {
        var pagedResult = new PagedResult<UserWithDeviceCount>
        {
            Items = new List<UserWithDeviceCount>
            {
                new UserWithDeviceCount { User = new User { KeyField = "u1" }, DeviceCount = 2 }
            },
            TotalCount = 1,
            Page = 1,
            PageSize = 25
        };

        _cqrsMock.Setup(c => c.SendQueryAsync<GetAllUsersPagedQueryResult>(It.IsAny<Query>()))
            .ReturnsAsync(new GetAllUsersPagedQueryResult { Success = true, Result = pagedResult });

        var sut = CreateSut();

        var actionResult = await sut.GetAll(new PagedRequest());

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var result = ok.Value.Should().BeAssignableTo<PagedResult<UserWithDeviceCount>>().Subject;
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAll_NormalizesRequest_ClampsPageSizeTo100()
    {
        // Capture the query that was sent to verify normalization happened
        GetAllUsersPagedQuery? capturedQuery = null;
        _cqrsMock.Setup(c => c.SendQueryAsync<GetAllUsersPagedQueryResult>(It.IsAny<Query>()))
            .Callback<Query>(q => capturedQuery = (GetAllUsersPagedQuery)q)
            .ReturnsAsync(new GetAllUsersPagedQueryResult
            {
                Success = true,
                Result = new PagedResult<UserWithDeviceCount>()
            });

        var sut = CreateSut();
        // Send an out-of-bounds pageSize — Normalize() must clamp it
        await sut.GetAll(new PagedRequest { Page = 0, PageSize = 9999 });

        capturedQuery.Should().NotBeNull();
        capturedQuery!.Page.Should().Be(1, "page 0 normalizes to 1");
        capturedQuery.PageSize.Should().Be(100, "pageSize 9999 clamps to 100");
    }

    [Fact]
    public async Task GetAll_WhenCqrsFailure_ReturnsBadRequest()
    {
        _cqrsMock.Setup(c => c.SendQueryAsync<GetAllUsersPagedQueryResult>(It.IsAny<Query>()))
            .ReturnsAsync(new GetAllUsersPagedQueryResult { Success = false, Message = "Error" });

        var actionResult = await CreateSut().GetAll(new PagedRequest());

        actionResult.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetAll_WhenException_Returns500()
    {
        _cqrsMock.Setup(c => c.SendQueryAsync<GetAllUsersPagedQueryResult>(It.IsAny<Query>()))
            .ThrowsAsync(new Exception("boom"));

        var actionResult = await CreateSut().GetAll(new PagedRequest());

        actionResult.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
    }

    // =========================================================================
    // GET /api/users/{keyType}/{keyValue}
    // =========================================================================

    [Fact]
    public async Task GetByKey_WhenUserFound_ReturnsOk()
    {
        var user = new User { KeyField = "abc123", FirstName = "Jane" };
        _cqrsMock.Setup(c => c.SendQueryAsync<GetUserByKeyQueryResult>(It.IsAny<Query>()))
            .ReturnsAsync(new GetUserByKeyQueryResult { Success = true, User = user });

        var actionResult = await CreateSut().GetByKey("User", "abc123");

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(user);
    }

    [Fact]
    public async Task GetByKey_WhenUserNotFound_ReturnsNotFound()
    {
        _cqrsMock.Setup(c => c.SendQueryAsync<GetUserByKeyQueryResult>(It.IsAny<Query>()))
            .ReturnsAsync(new GetUserByKeyQueryResult { Success = false, User = null, Message = "Not found" });

        var actionResult = await CreateSut().GetByKey("User", "missing");

        actionResult.Should().BeOfType<NotFoundObjectResult>();
    }

    // =========================================================================
    // POST /api/users
    // =========================================================================

    [Fact]
    public async Task Create_WhenSuccess_ReturnsCreated()
    {
        var userKey = new Key("User", "new-guid");
        _cqrsMock.Setup(c => c.SendCommandAsync<CreateUserCommandResult>(It.IsAny<Command>()))
            .ReturnsAsync(new CreateUserCommandResult
            {
                Success = true,
                Message = "User created successfully",
                UserKey = userKey
            });

        var request = new CreateUserRequest
        {
            KeycloakUserId = "kc-123",
            FirstName = "Bob",
            LastName = "Smith",
            Email = "bob@test.com",
            Role = UserRole.Self
        };

        var actionResult = await CreateSut().Create(request);

        actionResult.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task Create_WhenCqrsFailure_ReturnsBadRequest()
    {
        _cqrsMock.Setup(c => c.SendCommandAsync<CreateUserCommandResult>(It.IsAny<Command>()))
            .ReturnsAsync(new CreateUserCommandResult { Success = false, Message = "Duplicate email" });

        var actionResult = await CreateSut().Create(new CreateUserRequest());

        actionResult.Should().BeOfType<BadRequestObjectResult>();
    }

    // =========================================================================
    // PUT /api/users/{keyType}/{keyValue}
    // =========================================================================

    [Fact]
    public async Task Update_WhenSuccess_ReturnsOk()
    {
        _cqrsMock.Setup(c => c.SendCommandAsync<UpdateUserCommandResult>(It.IsAny<Command>()))
            .ReturnsAsync(new UpdateUserCommandResult { Success = true, Message = "Updated" });

        var actionResult = await CreateSut().Update("User", "abc", new UpdateUserRequest
        {
            FirstName = "Alice",
            LastName = "Smith",
            Address = "123 Main",
            City = "Boston",
            PhoneNumber = "555-1234"
        });

        actionResult.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Update_WhenCqrsFailure_ReturnsBadRequest()
    {
        _cqrsMock.Setup(c => c.SendCommandAsync<UpdateUserCommandResult>(It.IsAny<Command>()))
            .ReturnsAsync(new UpdateUserCommandResult { Success = false, Message = "User not found" });

        var actionResult = await CreateSut().Update("User", "missing", new UpdateUserRequest());

        actionResult.Should().BeOfType<BadRequestObjectResult>();
    }

    // =========================================================================
    // GET /api/users/{keyType}/{keyValue}/risk-score
    // =========================================================================

    [Fact]
    public async Task GetRiskScore_WhenScoreExists_ReturnsOkWithScore()
    {
        var score = new global::Common.Models.UserRiskScore { Score = 42, Level = "Low" };
        _cqrsMock.Setup(c => c.SendQueryAsync<GetLatestUserRiskScoreQueryResult>(It.IsAny<Query>()))
            .ReturnsAsync(new GetLatestUserRiskScoreQueryResult { Success = true, Score = score });

        var actionResult = await CreateSut().GetRiskScore("User", "abc");

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(score);
    }

    [Fact]
    public async Task GetRiskScore_WhenNoScoreYet_ReturnsOkWithNullScore()
    {
        _cqrsMock.Setup(c => c.SendQueryAsync<GetLatestUserRiskScoreQueryResult>(It.IsAny<Query>()))
            .ReturnsAsync(new GetLatestUserRiskScoreQueryResult { Success = true, Score = null });

        var actionResult = await CreateSut().GetRiskScore("User", "abc");

        actionResult.Should().BeOfType<OkObjectResult>();
    }

    // =========================================================================
    // GET /api/users/{keyType}/{keyValue}/alerts
    // =========================================================================

    [Fact]
    public async Task GetAlerts_NormalizesRequest()
    {
        GetUserAlertsByKeyQuery? capturedQuery = null;
        _cqrsMock.Setup(c => c.SendQueryAsync<GetUserAlertsByKeyQueryResult>(It.IsAny<Query>()))
            .Callback<Query>(q => capturedQuery = (GetUserAlertsByKeyQuery)q)
            .ReturnsAsync(new GetUserAlertsByKeyQueryResult
            {
                Success = true,
                Result = new PagedResult<global::Common.Entities.DeviceAlertEntity>()
            });

        await CreateSut().GetAlerts("User", "abc", new PagedRequest { Page = -1, PageSize = 200 });

        capturedQuery!.Page.Should().Be(1);
        capturedQuery.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task GetAlerts_SetsUserKeyOnQuery()
    {
        GetUserAlertsByKeyQuery? capturedQuery = null;
        _cqrsMock.Setup(c => c.SendQueryAsync<GetUserAlertsByKeyQueryResult>(It.IsAny<Query>()))
            .Callback<Query>(q => capturedQuery = (GetUserAlertsByKeyQuery)q)
            .ReturnsAsync(new GetUserAlertsByKeyQueryResult
            {
                Success = true,
                Result = new PagedResult<global::Common.Entities.DeviceAlertEntity>()
            });

        await CreateSut().GetAlerts("User", "target-key", new PagedRequest());

        capturedQuery!.UserKey.Type.Should().Be("User");
        capturedQuery.UserKey.Value.Should().Be("target-key");
    }

    // =========================================================================
    // GET /api/users/{keyType}/{keyValue}/devices
    // =========================================================================

    [Fact]
    public async Task GetDevices_ReturnsDeviceList()
    {
        var devices = new List<global::Common.Entities.UserDevice>
        {
            new global::Common.Entities.PersonalComputer { KeyField = "d1" }
        };
        _cqrsMock.Setup(c => c.SendQueryAsync<GetDevicesByUserQueryResult>(It.IsAny<Query>()))
            .ReturnsAsync(new GetDevicesByUserQueryResult { Success = true, Devices = devices });

        var actionResult = await CreateSut().GetDevices("User", "abc");

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<List<global::Common.Entities.UserDevice>>().Subject;
        list.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetDevices_WhenCqrsFailure_ReturnsBadRequest()
    {
        _cqrsMock.Setup(c => c.SendQueryAsync<GetDevicesByUserQueryResult>(It.IsAny<Query>()))
            .ReturnsAsync(new GetDevicesByUserQueryResult { Success = false, Message = "User not found" });

        var actionResult = await CreateSut().GetDevices("User", "missing");

        actionResult.Should().BeOfType<BadRequestObjectResult>();
    }
}
