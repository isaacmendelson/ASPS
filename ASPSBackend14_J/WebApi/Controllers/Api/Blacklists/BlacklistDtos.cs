namespace WebApi.Controllers.Api.Blacklists;

/// <summary>DTO for KnownPhishingWebsite entries returned by the Blacklists API.</summary>
public class PhishingWebsiteDto
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string? Source { get; set; }
    public DateTime DateCreated { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>DTO for BlacklistedPhoneNumber entries returned by the Blacklists API.</summary>
public class PhoneNumberDto
{
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public DateTime DateCreated { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>DTO for BankWebsite entries returned by the Blacklists API.</summary>
public class BankWebsiteDto
{
    public int Id { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string? Country { get; set; }
    public bool IsActive { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
}

/// <summary>DTO for WebsiteCategory entries returned by the Blacklists API.</summary>
public class WebsiteCategoryDto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string? ParentName { get; set; }
    public string? Source { get; set; }
    public DateTime DateCreated { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>Request body for creating a website category.</summary>
public class CreateWebsiteCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string? Source { get; set; }
}

/// <summary>Request body for updating a website category.</summary>
public class UpdateWebsiteCategoryRequest
{
    public string? ParentId { get; set; }
    public string? Source { get; set; }
}

/// <summary>DTO for TrackedDomain entries returned by the Blacklists API.</summary>
public class TrackedDomainDto
{
    public int Id { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? UserKey { get; set; }
    public string? ScamInProgressKey { get; set; }
    public string? AnalysisKey { get; set; }
    public int TrackMode { get; set; }
    public string? Reason { get; set; }
    public bool IsActive { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateModified { get; set; }
}

/// <summary>Request body for creating a tracked domain.</summary>
public class CreateTrackedDomainRequest
{
    public string Domain { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? UserKeyField { get; set; }
    public string? ScamInProgressKey { get; set; }
    public string? AnalysisKey { get; set; }
    public int TrackMode { get; set; } = 1;
    public string? Reason { get; set; }
    public bool NotifyAllUsers { get; set; }
}

/// <summary>Request body for updating a tracked domain.</summary>
public class UpdateTrackedDomainRequest
{
    public string? Domain { get; set; }
    public string? Category { get; set; }
    public string? UserKeyField { get; set; }
    public string? ScamInProgressKey { get; set; }
    public int TrackMode { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public string? Reason { get; set; }
}

/// <summary>Request body for the notify-user action on a tracked domain.</summary>
public class NotifyUserRequest
{
    public string UserKeyField { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
