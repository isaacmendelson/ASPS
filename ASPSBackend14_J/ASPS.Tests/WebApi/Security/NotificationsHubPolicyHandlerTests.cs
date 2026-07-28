using System.Security.Claims;
using Business.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WebApi.Security;
using WebApi.Services;

namespace ASPS.Tests.WebApi.Security;

public class NotificationsHubPolicyHandlerTests
{
    [Fact]
    public async Task HandleAsync_MissingCredentials_DoesNotAuthorize()
    {
        var cqrsClient = new Mock<ICQRSClient>(MockBehavior.Strict);
        var handler = CreateHandler(cqrsClient);
        var context = CreateContext(new ClaimsPrincipal(new ClaimsIdentity()), "/notificationshub");

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        cqrsClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_InvalidDeviceToken_DoesNotAuthorize()
    {
        var cqrsClient = new Mock<ICQRSClient>();
        cqrsClient
            .Setup(x => x.SendQueryAsync<ValidateDeviceTokenQueryResult>(
                It.Is<ValidateDeviceTokenQuery>(query =>
                    query.DeviceUid == "device-a" &&
                    query.TokenValue == "invalid")))
            .ReturnsAsync(new ValidateDeviceTokenQueryResult
            {
                Success = true,
                IsValid = false
            });
        var handler = CreateHandler(cqrsClient);
        var context = CreateContext(
            new ClaimsPrincipal(new ClaimsIdentity()),
            "/notificationshub?deviceUid=device-a&token=invalid");

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_ValidDeviceToken_AddsTrustedMembershipClaims()
    {
        var cqrsClient = new Mock<ICQRSClient>();
        cqrsClient
            .Setup(x => x.SendQueryAsync<ValidateDeviceTokenQueryResult>(
                It.Is<ValidateDeviceTokenQuery>(query =>
                    query.DeviceUid == "device-a" &&
                    query.TokenValue == "valid-token")))
            .ReturnsAsync(new ValidateDeviceTokenQueryResult
            {
                Success = true,
                IsValid = true,
                UserKeyField = "user-1"
            });
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var handler = CreateHandler(cqrsClient);
        var context = CreateContext(
            principal,
            "/notificationshub?deviceUid=device-a&token=valid-token");

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        Assert.Contains(
            principal.Identities,
            identity => identity.IsAuthenticated &&
                        identity.AuthenticationType == "DeviceToken");
        Assert.Equal("device-a", principal.FindFirstValue(HubClaimTypes.DeviceUid));
        Assert.Equal("user-1", principal.FindFirstValue(HubClaimTypes.UserKeyField));
        Assert.Equal(
            "client_device-a",
            principal.FindFirstValue(HubClaimTypes.NotificationGroup));
    }

    [Fact]
    public async Task HandleAsync_AdminPrincipal_AuthorizesWithoutDeviceToken()
    {
        var cqrsClient = new Mock<ICQRSClient>(MockBehavior.Strict);
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Role, "Admin") },
            authenticationType: "Test");
        var handler = CreateHandler(cqrsClient);
        var context = CreateContext(
            new ClaimsPrincipal(identity),
            "/notificationshub");

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        cqrsClient.VerifyNoOtherCalls();
    }

    private static NotificationsHubAuthorizationHandler CreateHandler(
        Mock<ICQRSClient> cqrsClient)
    {
        return new NotificationsHubAuthorizationHandler(
            cqrsClient.Object,
            NullLogger<NotificationsHubAuthorizationHandler>.Instance);
    }

    private static AuthorizationHandlerContext CreateContext(
        ClaimsPrincipal principal,
        string requestTarget)
    {
        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        var requestUri = new Uri($"https://localhost{requestTarget}");
        httpContext.Request.Path = requestUri.AbsolutePath;
        httpContext.Request.QueryString = new QueryString(requestUri.Query);

        return new AuthorizationHandlerContext(
            new[] { new NotificationsHubRequirement() },
            principal,
            httpContext);
    }
}
