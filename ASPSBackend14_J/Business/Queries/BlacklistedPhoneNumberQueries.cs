using Common.Entities;
using Common.Messaging;

namespace Business.Queries;

/// <summary>
/// Query to get all blacklisted phone numbers with optional search and pagination.
/// JIRA: ASPS-282
/// </summary>
public class GetAllBlacklistedPhoneNumbersQuery : Query
{
    public GetAllBlacklistedPhoneNumbersQuery()
    {
        QueryType = nameof(GetAllBlacklistedPhoneNumbersQuery);
    }

    public string QueryType { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? Search { get; set; }
}

public class GetAllBlacklistedPhoneNumbersQueryResult : QueryResult
{
    public List<BlacklistedPhoneNumber> PhoneNumbers { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// Query to get a single blacklisted phone number by ID.
/// JIRA: ASPS-282
/// </summary>
public class GetBlacklistedPhoneNumberByIdQuery : Query
{
    public GetBlacklistedPhoneNumberByIdQuery()
    {
        QueryType = nameof(GetBlacklistedPhoneNumberByIdQuery);
    }

    public string QueryType { get; set; }
    public int Id { get; set; }
}

public class GetBlacklistedPhoneNumberByIdQueryResult : QueryResult
{
    public BlacklistedPhoneNumber? PhoneNumber { get; set; }
}

/// <summary>
/// Query to check if a phone number is blacklisted.
/// JIRA: ASPS-282
/// </summary>
public class CheckPhoneNumberBlacklistedQuery : Query
{
    public CheckPhoneNumberBlacklistedQuery()
    {
        QueryType = nameof(CheckPhoneNumberBlacklistedQuery);
    }

    public string QueryType { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
}

public class CheckPhoneNumberBlacklistedQueryResult : QueryResult
{
    public bool IsBlacklisted { get; set; }
    public BlacklistedPhoneNumber? PhoneNumber { get; set; }
}
