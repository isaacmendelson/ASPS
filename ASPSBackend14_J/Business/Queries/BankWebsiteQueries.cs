using Common.Entities;
using Common.Messaging;

namespace Business.Queries;

/// <summary>
/// Query to get all bank websites with optional search and pagination.
/// JIRA: ASPS-297
/// </summary>
public class GetAllBankWebsitesQuery : Query
{
    public GetAllBankWebsitesQuery()
    {
        QueryType = nameof(GetAllBankWebsitesQuery);
    }

    public string QueryType { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
}

public class GetAllBankWebsitesQueryResult : QueryResult
{
    public List<BankWebsite> BankWebsites { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// Query to get a single bank website by ID.
/// JIRA: ASPS-297
/// </summary>
public class GetBankWebsiteByIdQuery : Query
{
    public GetBankWebsiteByIdQuery()
    {
        QueryType = nameof(GetBankWebsiteByIdQuery);
    }

    public string QueryType { get; set; }
    public int Id { get; set; }
}

public class GetBankWebsiteByIdQueryResult : QueryResult
{
    public BankWebsite? BankWebsite { get; set; }
}

/// <summary>
/// Query to check if a domain is a known bank website.
/// JIRA: ASPS-297
/// </summary>
public class CheckDomainIsBankQuery : Query
{
    public CheckDomainIsBankQuery()
    {
        QueryType = nameof(CheckDomainIsBankQuery);
    }

    public string QueryType { get; set; }
    public string Domain { get; set; } = string.Empty;
}

public class CheckDomainIsBankQueryResult : QueryResult
{
    public bool IsBank { get; set; }
    public BankWebsite? BankWebsite { get; set; }
}
