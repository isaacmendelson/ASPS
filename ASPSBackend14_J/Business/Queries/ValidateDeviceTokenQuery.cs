using Common.Messaging;

namespace Business.Queries;

public class ValidateDeviceTokenQuery : Query
{
    public ValidateDeviceTokenQuery()
    {
        QueryType = nameof(ValidateDeviceTokenQuery);
    }

    public string QueryType { get; set; }
    public string DeviceUid { get; set; } = string.Empty;
    public string TokenValue { get; set; } = string.Empty;
}

public class ValidateDeviceTokenQueryResult : QueryResult
{
    public bool IsValid { get; set; }
    public string? UserKeyField { get; set; }
}
