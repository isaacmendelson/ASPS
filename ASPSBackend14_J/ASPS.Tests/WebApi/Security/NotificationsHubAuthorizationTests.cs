using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WebApi.Hubs;
using WebApi.Security;

namespace ASPS.Tests.WebApi.Security;

public class NotificationsHubAuthorizationTests
{
    [Fact]
    public async Task SubscribeToNotifications_AnonymousCaller_CannotJoinArbitraryGroup()
    {
        var groups = new Mock<IGroupManager>(MockBehavior.Strict);
        var hub = CreateHub(new ClaimsPrincipal(new ClaimsIdentity()), groups);

        await Assert.ThrowsAsync<HubException>(
            () => hub.SubscribeToNotifications("victim-device"));

        groups.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SubscribeToNotifications_DeviceClaim_CannotJoinDifferentDeviceGroup()
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(HubClaimTypes.DeviceUid, "device-a"),
                new Claim(HubClaimTypes.NotificationGroup, "client_device-a")
            },
            authenticationType: "DeviceToken");
        var groups = new Mock<IGroupManager>(MockBehavior.Strict);
        var hub = CreateHub(new ClaimsPrincipal(identity), groups);

        await Assert.ThrowsAsync<HubException>(
            () => hub.SubscribeToNotifications("device-b"));

        groups.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SubscribeToNotifications_DeviceClaim_CanJoinOwnDeviceGroup()
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(HubClaimTypes.DeviceUid, "device-a"),
                new Claim(HubClaimTypes.NotificationGroup, "client_device-a")
            },
            authenticationType: "DeviceToken");
        var groups = new Mock<IGroupManager>(MockBehavior.Strict);
        groups
            .Setup(x => x.AddToGroupAsync("connection-1", "client_device-a", default))
            .Returns(Task.CompletedTask);
        var hub = CreateHub(new ClaimsPrincipal(identity), groups);

        await hub.SubscribeToNotifications("device-a");

        groups.VerifyAll();
    }

    [Fact]
    public async Task UnsubscribeFromNotifications_DeviceClaim_CannotLeaveDifferentDeviceGroup()
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(HubClaimTypes.DeviceUid, "device-a"),
                new Claim(HubClaimTypes.NotificationGroup, "client_device-a")
            },
            authenticationType: "DeviceToken");
        var groups = new Mock<IGroupManager>(MockBehavior.Strict);
        var hub = CreateHub(new ClaimsPrincipal(identity), groups);

        await Assert.ThrowsAsync<HubException>(
            () => hub.UnsubscribeFromNotifications("device-b"));

        groups.VerifyNoOtherCalls();
    }

    private static NotificationsHub CreateHub(
        ClaimsPrincipal user,
        Mock<IGroupManager> groups)
    {
        var context = new Mock<HubCallerContext>();
        context.SetupGet(x => x.User).Returns(user);
        context.SetupGet(x => x.ConnectionId).Returns("connection-1");
        context.SetupGet(x => x.Items).Returns(new Dictionary<object, object?>());

        return new NotificationsHub(
            NullLogger<NotificationsHub>.Instance)
        {
            Context = context.Object,
            Groups = groups.Object
        };
    }
}
