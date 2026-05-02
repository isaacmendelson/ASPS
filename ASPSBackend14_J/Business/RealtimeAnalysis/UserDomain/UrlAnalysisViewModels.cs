using Business.RealtimeAnalysis.Indicators;
using Business.Views;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Policy;
using System.ServiceModel.Channels;
using System.Text.Json.Serialization;

namespace Business.RealtimeAnalysis.UserDomain;

/// <summary>
/// Represents an external Python analyzer module.
/// Each analyzer is a subdirectory containing an analyze.py script.
/// </summary>
public class ExternalAnalyzer
{
    /// <summary>
    /// Directory name inside Analyzers folder (e.g., "basic-url-analyzer").
    /// The system will call "analyze.py" inside this directory.
    /// </summary>
    public string ScriptFile { get; set; } = string.Empty;
    
    /// <summary>
    /// Execution order (lower values run first).
    /// </summary>
    public int Order { get; set; }
    
    /// <summary>
    /// Weight for result aggregation (higher = more important).
    /// </summary>
    public float Weight { get; set; }
}

public class ErrorMessage
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

public class AnalysisResult : IAnalysisResult
{
    [DataMember] 
    public bool Success { get; set; } = true;
    [DataMember]
    public ErrorMessage? Error { get; set; }

    [DataMember]
    public string? ResultId { get; set; }
    //public RiskAssessmentVm? risk_assessment { get; set; }

    [DataMember]
    public DateTime analyzed_at { get; set; }

    //[DataMember]
    //public  string TypeName => "AnalysisResult";

}

[Serializable]
[DataContract]
public class AnalyzerResultVm
{
    protected AnalyzerResultVm() 
    {
    }
    public AnalyzerResultVm(
        //AnalysisResult analysisResult, 
        Indicator[] indicators, IProtectiveAction[] protectiveActions, string analyzerName, string deviceUid, DateTime timestamp, Severity severity)
    {
        //AnalysisResult = analysisResult;
        //switch (analysisResult)
        //{
        //    case UrlAnalysisResult u:
        //        AnalysisResult = u ;
        //        break;
        //    case RemoteAccessAnalysisResult r:
        //        analysisResult = r;
        //        break;
        //}
        Indicators = indicators;
        ProtectiveActions = protectiveActions;
        this.AnalyzerName = analyzerName;
        this.DeviceUid = deviceUid;
        Timestamp = timestamp;
        Severity = severity;
    }

    [DataMember]
    public string TypeName => "AnalyzerResultVm";
    [DataMember]
    public Severity Severity { get; set; }
    //[DataMember] 
    //public virtual IAnalysisResult AnalysisResult { get; set; }
    [DataMember] 
    public IIndicator[] Indicators { get; set; }
    [DataMember] 
    public IProtectiveAction[] ProtectiveActions { get; set; }

    [DataMember]
    public string? AnalyzerName { get; set; }

    [DataMember]
    public string? DeviceUid { get; set; }
    [DataMember]
    public DateTime Timestamp { get; set; }
}

[Serializable]
[DataContract]
public class UrlAnalyzerResultVm : AnalyzerResultVm
{
    public UrlAnalyzerResultVm(
        UrlAnalysisResult analysisResult, 
        Indicator[] indicators, ProtectiveAction[] protectiveActions, string analyzerName, string deviceUid, DateTime timestamp, Severity severity)
        : base(indicators, protectiveActions, analyzerName, deviceUid, timestamp, severity)
    {
        AnalysisResult = analysisResult;
    }
    [DataMember]
    public UrlAnalysisResult AnalysisResult { get; set; }
}

[Serializable]
[DataContract]
public class TrackUrlAnalyzerResultVm : AnalyzerResultVm
{
    public TrackUrlAnalyzerResultVm(
        TrackUrlAnalysisResult analysisResult,
        Indicator[] indicators, ProtectiveAction[] protectiveActions, string analyzerName, string deviceUid, DateTime timestamp, Severity severity)
        : base(indicators, protectiveActions, analyzerName, deviceUid, timestamp, severity)
    {
        AnalysisResult = analysisResult;
    }
    [DataMember]
    public TrackUrlAnalysisResult AnalysisResult { get; set; }
}

[Serializable]
[DataContract]
public class RemoteAccessAnalyzerResultVm : AnalyzerResultVm
{
    public RemoteAccessAnalyzerResultVm(
        RemoteAccessAnalysisResult analysisResult,
        Indicator[] indicators, ProtectiveAction[] protectiveActions, string analyzerName, string deviceUid, DateTime timestamp, Severity severity)
        : base(indicators, protectiveActions, analyzerName, deviceUid, timestamp, severity)
    {
        AnalysisResult = analysisResult;
    }
    [DataMember]
    public RemoteAccessAnalysisResult AnalysisResult { get; set; }
}




[Serializable]
[DataContract]
public class RemoteAccessAnalysisResult : AnalysisResult
{
    public RemoteAccessAnalysisResult(RemoteAccessApp remoteAccessApp, int runningProcesses, string connectionUrl, ConnectionStatus connectionStatus,
        RemoteAccessDirection remoteAccessDirection, int connectionsCount, int sessionStatus, BrowserTab[]? browserTabs, RiskAssessment? risk_assessment)
    {
        this.RemoteAccessApp = remoteAccessApp;
        this.RunningProcesses = runningProcesses;
        this.ConnectionUrl = connectionUrl;
        this.ConnectionStatus = connectionStatus;
        this.ConnectionsCount = connectionsCount;
        this.SessionStatus = sessionStatus;
        this.risk_assessment = risk_assessment;
        this.BrowserTabs = browserTabs;
        this.RemoteAccessDirection = remoteAccessDirection;
    }

    protected  RemoteAccessAnalysisResult() { }

    [DataMember]
    public string TypeName => "RemoteAccessAnalysisResult";

    [DataMember]
    public RemoteAccessApp RemoteAccessApp { get; set; }
    [DataMember]
    public RemoteAccessDirection RemoteAccessDirection { get; set; }
    [DataMember] 
    public int RunningProcesses { get; set; }
    [DataMember] 
    public string ConnectionUrl { get; set; } = string.Empty;
    [DataMember] 
    public ConnectionStatus ConnectionStatus { get; set; }
    [DataMember] 
    public int ConnectionsCount { get; set; }
    [DataMember] 
    public int SessionStatus { get; set; }
    [DataMember]
    public BrowserTab[]? BrowserTabs { get; set; }

    [DataMember]
    public RiskAssessment? risk_assessment { get; set; }
}

public class Reputation
{

    public bool Success { get; set; }
    public bool IsWellKnown { get; set; }

    public long ReputableMentions { get; set; }
    public string[] MentionSources { get; set; }

    public float ReputationScore { get; set; }

    public float ScoreAdjustment { get; set; }

}
public class WebsiteCategoryResult
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    protected WebsiteCategoryResult() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    public WebsiteCategoryResult(WebsiteCategory category, WebsiteCategory categoryGroup, string nameEn, float confidence, string detectionMethod, MatchedSignal[] matchedSignals)
    {
        Category = new WebsiteCategoryView(category);
        CategoryGroup = new WebsiteCategoryView(categoryGroup);
        NameEn = nameEn;
        Confidence = confidence;
        DetectionMethod = detectionMethod;
        MatchedSignals = matchedSignals;
    }

    public WebsiteCategoryResult(WebsiteCategoryResultVm vm)
    {
        this.Category = new WebsiteCategoryView(vm.category, vm.category_group, vm.detection_method);
        this.CategoryGroup = new WebsiteCategoryView(vm.category_group,null, vm.detection_method);
        this.DetectionMethod = vm.detection_method;
        this.Confidence = vm.confidence;
        this.MatchedSignals = vm.matched_signals.Select(i => new MatchedSignal(i)).ToArray();
        this.NameEn = vm.name_en;

    }

    public WebsiteCategoryView Category { get; set; }
    public WebsiteCategoryView CategoryGroup { get; set; }
    public string NameEn { get; set; }
    public float Confidence { get; set; }
    public string DetectionMethod { get; set; }
    public MatchedSignal[] MatchedSignals { get; set; }
}

public class WebsiteCategoryResultVm
{
    protected WebsiteCategoryResultVm() { }
    public WebsiteCategoryResultVm(string category, string category_group, string name_en, float confidence, string detection_method, MatchedSignalVm[] matched_signals)
    {
        this.category = category;
        this.category_group = category_group;
        this.name_en = name_en;
        this.confidence = confidence;
        this.detection_method = detection_method;
        this.matched_signals = matched_signals;
    }


    public string category { get; set; }
    public string category_group { get; set; }
    public string name_en { get; set; }
    public float confidence { get; set; }
    public string detection_method { get; set; }
    public MatchedSignalVm[] matched_signals { get; set; }
}

public class  MatchedSignal
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    protected MatchedSignal() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    public MatchedSignal(string type, string value, float weight, string segment)
    {
        Type = type;
        Value = value;
        Weight = weight;
        Segment = segment;
    }
    public MatchedSignal(MatchedSignalVm vm)
    {
        this.Value = vm.value;
        this.Weight = vm.weight;
        this.Segment = vm.segment;
        this.Type = vm.type;
    }

    public string Type { get; set; }
    public string Value { get; set; }
    public float Weight { get; set; }
    public string Segment { get; set; }
}
public class MatchedSignalVm
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    protected MatchedSignalVm() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    public MatchedSignalVm(string type, string value, float weight, string segment)
    {
        this.type = type;
        this.value = value;
        this.weight = weight;
        this.segment = segment;
    }

    public string type { get; set; }
    public string value { get; set; }
    public float weight { get; set; }
    public string segment { get; set; }
}

//public class RiskAssessmentVm
//{
//    public RiskAssessmentVm(float risk_score, string risk_level, bool is_scam, float confidence)
//    {
//        this.risk_score = risk_score;
//        this.risk_level = risk_level;
//        this.is_scam = is_scam;
//        this.confidence = confidence;
//    }

//    public float risk_score { get; set; }
//    public string risk_level { get; set; } = string.Empty;
//    public bool is_scam { get; set; }
//    public float confidence { get; set; }
//}

public class Purpose
{
    /// <summary>
    /// Website category name (e.g., "banking", "ecommerce").
    /// Replaces WebsiteType enum - use ASView.GetCategoryView(categoryName) to get full category details.
    /// JIRA: SCRUM-821
    /// </summary>
    public string CategoryName { get; set; } = "unknown";
    
    public float Confidence { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class Whois
{

    protected Whois() { }

    public Whois(WhoisVm vm)
    {
        this.Country = vm.country;
        this.RiskScore = vm.risk_score;
        this.Registrar = vm.Registrar;
        this.Success = vm.Success;
        this.CreatedAt = vm.created_date;
        this.DomainAgeDays = vm.domain_age_days;
    }
    public bool Success { get; set; }
    public int DomainAgeDays { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string Registrar { get; set; }
    public string Country { get; set; }
    public bool IsPrivacyProtected { get; set; }

    public float RiskScore { get; set; }
}

public class WhoisVm
{
    public bool Success { get; set; }
    public int domain_age_days { get; set; }
    public DateTime? created_date { get; set; }
    public string Registrar { get; set; } = string.Empty;
    public string country { get; set; } = string.Empty;
    public bool privacy_protected { get; set; }
    public float risk_score { get; set; }
}


public class DetectedPattern
{
    protected DetectedPattern() { }

    public DetectedPattern(DetectedPatternVm vm)
    {
        this.Name = vm.Name;
        this.Type = vm.Type;
        this.MatchText = vm.matched_text;
        this.Weight = vm.Weight;
        this.Description = vm.Description;
    }
    public string Name { get; set; }
    public string Type { get; set; }
    public string MatchText { get; set; }
    public float Weight { get; set; }
    public string Description { get; set; }
}


public class ContentAnalysis
{
    protected ContentAnalysis() { }

    public ContentAnalysis(ContentAnalysisVm vm)
    {
        this.RiskScore = vm.Risk_Score;
        this.CtaCount = vm.cta_count;
        this.Title = vm.Title;
        this.Success = vm.Success;
        this.DetectedPatterns = vm.detected_patterns.Select(i => new DetectedPattern(i)).ToList();
        this.FormTypes = vm.form_types;
        this.HasSuspiciousKeywords = vm.HasSuspiciousKeywords;
        this.WordCount = vm.word_count;
        this.IsEnglish = vm.is_english;
    }
    public bool Success { get; set; }
    public string Title { get; set; }

    public bool? HasSuspiciousKeywords { get; set; } = false;
    public float RiskScore { get; set; }

    public List<DetectedPattern> DetectedPatterns { get; set; }
    public int CtaCount { get; set; }

    public List<int> FormTypes { get; set; }
    public int WordCount { get; set; }
    public bool? IsEnglish { get; set; }
}

public class ContentAnalysisVm
{
    public bool Success { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool? HasSuspiciousKeywords { get; set; } = false;
    public float Risk_Score { get; set; }
    public List<DetectedPatternVm>? detected_patterns { get; set; }
    public int cta_count { get; set; }
    public List<int> form_types { get; set; } = new List<int>();
    public int word_count { get; set; }
    public bool? is_english { get; set; }
}

public class ScrapingStatusVm
{
    public bool Success { get; set; }
    public int status_code { get; set; }
    public string final_url { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public string Category { get; set; } = string.Empty;
}

public class DetectedPatternVm
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string matched_text { get; set; } = string.Empty;
    public float Weight { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class MlAnalysis
{
    protected MlAnalysis() { }
    //public MlAnalysis(MlAnalysisVm vm)
    //{
    //    this.Success = vm.Success;
    //    this.Enabled = vm.Enabled;
    //    this.Score = vm.Score;
    //    this.Confidence = vm.Confidence;
    //    this.Note = vm.Note;
    //}

    public MlAnalysis(bool success, bool enabled, float score, float confidence, string note)
    {
        Success = success;
        Enabled = enabled;
        Score = score;
        Confidence = confidence;
        Note = note;
    }

    public bool Success { get; set; }
    public bool Enabled { get; set; }
    public float Score { get; set; }
    public float Confidence { get; set; }
    public string Note { get; set; } = string.Empty;
}

/// <summary>
/// Result of checking URL/Domain against known phishing database
/// </summary>
/// 
[Serializable]
[DataContract]
public class PhishingCheckResult
{
    protected PhishingCheckResult() { }

    public PhishingCheckResult(PhishingCheckResultVm vm)
    {
        this.IsKnownPhishing = vm.Is_known_phishing;
        this.IsKnownPhishingDomain = vm.Is_known_phishing_domain;
        this.Source = vm.Source?? PhishingCheckResultSource.Unknown;
        this.MatchCount = vm.Match_count;
        this.CheckedAt = vm.Checked_at;
    }

    /// <summary>
    /// True if exact URL match found in phishing database
    /// </summary>
    public bool IsKnownPhishing { get; set; }

    /// <summary>
    /// True if domain found in phishing database (even if URL doesn't match exactly)
    /// </summary>
    public bool IsKnownPhishingDomain { get; set; }

    /// <summary>
    /// Source of the phishing URL (e.g., "PhishTank", "OpenPhish", "Manual")
    /// </summary>
    public PhishingCheckResultSource? Source { get; set; }

    /// <summary>
    /// Number of phishing URLs found for this domain
    /// </summary>
    public int MatchCount { get; set; }

    /// <summary>
    /// When the check was performed
    /// </summary>
    public DateTime CheckedAt { get; set; }
}

/// <summary>
/// View model for phishing check results from database
/// </summary>
public class PhishingCheckResultVm
{
    public bool Is_known_phishing { get; set; }
    public bool Is_known_phishing_domain { get; set; }
    public PhishingCheckResultSource? Source { get; set; }
    public int Match_count { get; set; }
    public DateTime Checked_at { get; set; }
}

public enum PhishingCheckResultSource
{
    Unknown = 0,
    KnownList = 1,
    Self = 2
}

/// <summary>
/// TrackedDomain information for a matched domain
/// </summary>
[Serializable]
[DataContract]
public class TrackedDomainInfo
{
    public TrackedDomainInfo(int id, string domain, string category, bool isExactMatch)
    {
        Id = id;
        Domain = domain;
        Category = category;
        IsExactMatch = isExactMatch;
    }

    protected TrackedDomainInfo() { }

    [DataMember]
    public int Id { get; set; }

    [DataMember]
    public string Domain { get; set; } = string.Empty;

    [DataMember]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// True if URL domain exactly matches TrackedDomain, false if subdomain match
    /// </summary>
    [DataMember]
    public bool IsExactMatch { get; set; }
}



