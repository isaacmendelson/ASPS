using System.Security.Claims;
using Business.Queries;
using Microsoft.AspNetCore.Authorization;
using WebApi.Services;

namespace WebApi.Security;

public sealed class NotificationsHubRequirement : IAuthorizationRequirement;

public sealed class NotificationsHubAuthorizationHandler
    : AuthorizationHandler<NotificationsHubRequirement>
{
    private readonly ICQRSClient _cqrsClient;
    private readonly ILogger<NotificationsHubAuthorizationHandler> _logger;

    public NotificationsHubAuthorizationHandler(
        ICQRSClient cqrsClient,
        ILogger<NotificationsHubAuthorizationHandler> logger)
    {
        _cqrsClient = cqrsClient;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        NotificationsHubRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return;
        }

        if (context.Resource is not HttpContext httpContext)
        {
            return;
        }

        var deviceUid = httpContext.Request.Query["deviceUid"].ToString();
        var token = httpContext.Request.Query["token"].ToString();
        if (string.IsNullOrWhiteSpace(deviceUid) ||
            string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        try
        {
            var result =
                await _cqrsClient.SendQueryAsync<ValidateDeviceTokenQueryResult>(
                    new ValidateDeviceTokenQuery
                    {
                        DeviceUid = deviceUid,
                        TokenValue = token
                    });

            if (!result.Success ||
                !result.IsValid ||
                string.IsNullOrWhiteSpace(result.UserKeyField))
            {
                _logger.LogWarning(
                    "SignalR authorization rejected device {DeviceUid}",
                    deviceUid);
                return;
            }

            var identity = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, deviceUid),
                    new Claim(HubClaimTypes.DeviceUid, deviceUid),
                    new Claim(HubClaimTypes.UserKeyField, result.UserKeyField),
                    new Claim(
                        HubClaimTypes.NotificationGroup,
                        $"client_{deviceUid}")
                },
                authenticationType: "DeviceToken",
                nameType: ClaimTypes.NameIdentifier,
                roleType: ClaimTypes.Role);

            context.User.AddIdentity(identity);
            context.Succeed(requirement);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "SignalR authorization failed while validating device {DeviceUid}",
                deviceUid);
        }
    }
}
