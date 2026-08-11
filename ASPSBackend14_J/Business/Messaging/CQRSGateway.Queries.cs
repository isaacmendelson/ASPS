using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Business.Messaging;

/// <summary>
/// CQRS Gateway — query dispatch. Handler dispatch is delegated to
/// CqrsHandlerRegistry (ASPS-680/ASPS-681 Messaging Refactoring, Phase 1).
/// </summary>
public partial class CQRSGateway
{
    private async Task<string> ProcessQueryAsync(string messageJson, JObject jObject)
    {
        using var scope = _serviceProvider.CreateScope();

        // Get query type from JSON
        var queryType = jObject["QueryType"]?.ToString();

        if (string.IsNullOrEmpty(queryType))
        {
            return CreateErrorResponse("QueryType field is missing or empty");
        }

        _logger.LogInformation("Handling query: {QueryType}", queryType);

        return await _registry.DispatchAsync(queryType, messageJson, scope);
    }
}
