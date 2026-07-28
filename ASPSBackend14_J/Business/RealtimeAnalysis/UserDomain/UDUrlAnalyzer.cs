using Business.DomainEvents;
using Business.RealtimeAnalysis.Indicators;
using Business.Views;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using Common.Generated.Messaging.V1;
using Interface.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis.UserDomain;

public class UDUrlAnalyzer : ISpecificAnalyzer, IDomainEventHandler
{
    private readonly ILogger<UDUrlAnalyzer> _logger;
    private IConfiguration _configuration;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IKnownPhishingWebsiteRepository? _directPhishingRepo;
    private readonly ASView _asView;
    private string _pythonPath;
    private string _analyzersFolder;
    private int _riskThreshold ;
    private int _trackingDurationMinutes;
    private float _severityScoreThresholdCritical;
    private float _severityScoreThresholdHigh;
    private float _severityScoreThresholdMedium;

    public ExternalAnalyzer[] ExternalAnalyzers { get; }

    public UDUrlAnalyzer(
        ILogger<UDUrlAnalyzer> logger,
        IConfiguration configuration,
        IServiceScopeFactory serviceScopeFactory,
        ASView asView)
    {
        this._logger = logger;
        this.LoadConfiguration(configuration);
        _serviceScopeFactory = serviceScopeFactory;
        this._asView = asView;

        // Define external analyzers
        ExternalAnalyzers = new[]
        {
            new ExternalAnalyzer
            {
                ScriptFile = "basic-url-analyzer",  // Directory name, not file name
                Order = 1,
                Weight = 1.0f
            }
        };

        _logger.LogInformation($"UDUrlAnalyzer initialized with {ExternalAnalyzers.Length} external analyzers");
        _logger.LogInformation($"Python path: {_pythonPath}");
        _logger.LogInformation($"Analyzers folder: {_analyzersFolder}");
    }

    public bool CanAnalyze(DeviceAlert alert)
    {
        return alert is UrlAlert;
    }

    public async Task<AnalyzerResult> AnalyzeAsync(DeviceAlert alert, List<DeviceAlert> historicalAlerts, IConfiguration configuration)
    {
        if (alert is not UrlAlert urlAlert)
        {
            return new AnalyzerResult(Severity.Low, "Alert is not a UrlAlert");
        }

        string url = urlAlert.Url;

        _logger.LogInformation($"Analyzing URL: {url}");

        // STEP 0: Check if domain is whitelisted (SafeDomains)
        var alertDomain = Common.Entities.KnownPhishingWebsite.GetDomainFromUrl(url);
        if (!string.IsNullOrEmpty(alertDomain) && _asView.IsSafeDomain(alertDomain))
        {
            _logger.LogInformation($"Domain '{alertDomain}' is whitelisted (SafeDomains). Skipping analysis.");

            var whitelistedResult = new UrlAnalysisResult
            {
                Url = url,
                Domain = alertDomain,
                analysis_time_ms = 0,
                IsFromCache = false,
                IsWhitelisted = true,
                Purpose = null,
                Whois = null,
                content_analysis = null,
                ml_analysis = null,
                phishing_check = null,
                red_flags = Array.Empty<string>(),
                Recommendation = "this domain is safe (whitelisted)",
                scraping_status = null,
                risk_assessment = new RiskAssessment(0, "Whitelisted", false, 1),
                website_category = null,
                Reputation = null,
                Warnings = Array.Empty<string>(),
                missing_data = Array.Empty<string>(),
                IsSensitiveWebsite = false
            };

            return new AnalyzerResult(
                Severity.Low,
                $"Domain '{alertDomain}' is whitelisted - skipping analysis",
                new List<IIndicator>(),
                new List<IProtectiveAction>(),
                new Dictionary<string, object>
                {
                    ["results"] = new[] { whitelistedResult },
                    ["errors"] = Array.Empty<string>(),
                    ["url"] = url,
                    ["analyzers_run"] = 0,
                    ["analyzers_total"] = ExternalAnalyzers.Length,
                    ["is_whitelisted"] = true
                }
            );
        }

        // STEP 1: Check against known phishing database FIRST (fast check)
        var phishingCheckResult = await CheckKnownPhishingAsync(url);

        // STEP 2: Continue with normal external analyzers
        var results = new List<UrlAnalysisResult>();
        var errors = new List<string>();

        // Execute each external analyzer
        foreach (var analyzer in ExternalAnalyzers.OrderBy(a => a.Order))
        {
            try
            {
                var result = new UrlAnalysisResult();
                _logger.LogInformation($"Running analyzer: {analyzer.ScriptFile} (Order: {analyzer.Order}, Weight: {analyzer.Weight})");

                // STEP 2: Check if a UrlAnalysisResult exists for this URL (cache)
                var cacheEnabled = configuration.GetValue<bool>("Analysis:CacheEnabled", true);
                if (cacheEnabled && this._asView.TryGetCachedUrlAnalysis(url, 0, out var cachedResult) && cachedResult is not null)
                {
                    _logger.LogInformation($"Cache hit for URL: {url}");
                    cachedResult.IsFromCache = true;

                    // Add phishing check result to cached result
                    cachedResult.AnalysisResult.phishing_check = phishingCheckResult;

                    result = new UrlAnalysisResult()
                    {
                        Url = url,
                        Domain = Common.Entities.KnownPhishingWebsite.GetDomainFromUrl(url),
                        analysis_time_ms = 0,
                        IsFromCache = true,
                        IsWhitelisted = false,
                        Purpose = cachedResult.AnalysisResult.Purpose,
                        Whois = cachedResult.AnalysisResult.Whois,
                        content_analysis = cachedResult.AnalysisResult.content_analysis,
                        ml_analysis = cachedResult.AnalysisResult.ml_analysis,
                        analyzed_at = cachedResult.AnalysisResult.analyzed_at,
                        phishing_check = phishingCheckResult,
                        red_flags = cachedResult.AnalysisResult.red_flags,
                        Recommendation = cachedResult.AnalysisResult.Recommendation,
                        scraping_status = cachedResult.AnalysisResult.scraping_status,
                        Error = cachedResult.AnalysisResult.Error,
                        risk_assessment = cachedResult.AnalysisResult.risk_assessment,
                        website_category = cachedResult.AnalysisResult.website_category,
                        Reputation = cachedResult.AnalysisResult.Reputation,
                        missing_data = cachedResult.AnalysisResult.missing_data,
                        Warnings = cachedResult.AnalysisResult.Warnings,
                        Success =  cachedResult.AnalysisResult.Success
                    };
                }
                else
                {
                    result = await RunPythonAnalyzerV1Async(analyzer, urlAlert);
                }

                if (result != null)
                {
                    result.phishing_check = phishingCheckResult;

                    // Compute IsSensitiveWebsite once per analysis. Primary source:
                    // SensitiveSites table (covers banks/crypto/government even on
                    // first visit). Fallback: classifier's website_category.
                    var sensitiveByTable = _asView.IsSensitiveDomain(url);
                    var category = result.website_category?.category?.ToLower();
                    var sensitiveByCategory = category is "bank" or "banking" or "crypto_exchange";
                    result.IsSensitiveWebsite = sensitiveByTable || sensitiveByCategory;

                    results.Add(result);
                    _logger.LogInformation($"Analyzer {analyzer.ScriptFile} completed successfully. Risk Score: {result.risk_assessment?.risk_score ?? 0}");
                    
                    // SCRUM-823: Auto-detect new WebsiteCategory from UrlAnalysisResult
                    await DetectAndCreateNewWebsiteCategoryAsync(result);
                }
                else
                {
                    errors.Add($"Analyzer {analyzer.ScriptFile} returned null");
                    _logger.LogWarning($"Analyzer {analyzer.ScriptFile} returned null");
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Analyzer {analyzer.ScriptFile} failed: {ex.Message}";
                errors.Add(errorMsg);
                _logger.LogError(ex, errorMsg);
                // Continue with next analyzer (don't stop on error)
            }
        }


        // STEP 4: Determine overall severity based on results
        var severity = Severity.Low;

        if (results.Any())
        {
            // STEP 3: Add phishing check to results (if we got Python analyzer results)
            var firstResult = results.First();
            firstResult.phishing_check = phishingCheckResult;

            var maxRiskScore = results
                .Where(r => r.risk_assessment != null)
                .Max(r => r.risk_assessment!.risk_score);

            var isScam = results
                .Where(r => r.risk_assessment != null)
                .Any(r => r.risk_assessment!.is_scam);

            severity = CalculateSeverity(phishingCheckResult, maxRiskScore, isScam);

            // STEP 5: Create indicators list
            var indicators = new List<IIndicator>();

            // Add phishing indicator if domain is suspicious
            if (phishingCheckResult.Is_known_phishing_domain)
            {
                var domain = Common.Entities.KnownPhishingWebsite.GetDomainFromUrl(url);
                var phishingIndicator = new KnownPhishingIndicator(
                    url,
                    domain,
                    phishingCheckResult.Is_known_phishing,
                    phishingCheckResult.Is_known_phishing_domain,
                    phishingCheckResult.Source,
                    phishingCheckResult.Match_count,
                    new BooleanScore(true, 1.0f, true),
                    AnalysisLevel.Device,
                    1,
                    1.0f
                );
                indicators.Add(phishingIndicator);
            }

            //Create ProtectiveActions
            List<IProtectiveAction> protectiveActions = new();

            if (indicators.Count > 0)
            {
                if (indicators.Any(i => i is KnownPhishingIndicator knownPhishingIndicator))
                {
                    string msg = $"Known phishing URL detected: ";

                    var action = new ProtectiveAction(
                        //ProtectiveActionSubject.Device, 
                        alert.DeviceInfo.Key,
                        ProtectiveActionType.UserDisplayNotification, AnalysisLevel.Device, msg, alert.AlertId);
                    protectiveActions.Add(action);
                }
            }

            // Check if URL tracking should be enabled based on risk threshold


            //////// Enable URL tracking if risk score exceeds threshold (higher score = higher risk)
            //////// Risk score of 40 or above means elevated risk, enable tracking
            //////if (maxRiskScore >= _riskThreshold)
            //////{
            //////    var domain = Common.Entities.KnownPhishingWebsite.GetDomainFromUrl(url);
            //////    _logger.LogInformation($"⚠️ Risk threshold exceeded for {domain}. Risk score: {maxRiskScore} >= {_riskThreshold}. Enabling URL tracking for {_trackingDurationMinutes} minutes.");

            //////    var trackingAction = new ProtectiveAction(
            //////        //ProtectiveActionSubject.Device,
            //////        alert.DeviceInfo.Key,
            //////        ProtectiveActionType.EnableUrlTracking,
            //////        AnalysisLevel.Device,
            //////        $"EnableUrlTracking|{domain}|{_trackingDurationMinutes}",
            //////        alert.AlertId);
            //////    protectiveActions.Add(trackingAction);
            //////}

            var analyzerResult = new AnalyzerResult
            (
                severity,
                results.Any()
                    ? $"URL analysis completed: {results.Count}/{ExternalAnalyzers.Length} analyzers succeeded"
                    : "All URL analyzers failed",
                indicators,  // Pass indicators list
                protectiveActions,
                new Dictionary<string, object>
                {
                    ["results"] = results.ToArray(),
                    ["errors"] = errors.ToArray(),
                    ["url"] = url,
                    ["timestamp"] = DateTime.UtcNow,
                    ["analyzers_run"] = results.Count,
                    ["external_analyzers_total"] = ExternalAnalyzers.Length
                }
            );

            var avgRiskScore = results
                .Where(r => r.risk_assessment != null)
                .Average(r => r.risk_assessment!.risk_score);

            analyzerResult.Details["aggregate_risk_score"] = avgRiskScore;
            analyzerResult.Details["is_scam"] = isScam;

            return analyzerResult;
        }
        else
        {
            _logger.LogWarning($"No valid results from analyzers for URL: {url}. Errors: {string.Join("; ", errors)}");
            return new AnalyzerResult(
                severity,
                $"No valid results from analyzers. Errors: {string.Join("; ", errors)}",
                new List<IIndicator>(),
                new List<IProtectiveAction>(),
                new Dictionary<string, object>
                {
                    ["results"] = Array.Empty<UrlAnalysisResult>(),
                    ["errors"] = errors.ToArray(),
                    ["url"] = url,
                    ["analyzers_run"] = 0,
                    ["analyzers_total"] = ExternalAnalyzers.Length
                }
            );
        }
    }

    public UDUrlAnalyzer(
        ILogger<UDUrlAnalyzer> logger,
        IConfiguration configuration,
        IKnownPhishingWebsiteRepository phishingRepo,
        ASView asView)
    {
        _logger = logger;
        LoadConfiguration(configuration);
        _directPhishingRepo = phishingRepo;
        _asView = asView;
        ExternalAnalyzers = new[] { new ExternalAnalyzer { ScriptFile = "basic-url-analyzer", Order = 1, Weight = 1.0f } };
    }

    private async Task<UrlAnalysisResult?> RunPythonAnalyzerV1Async(
        ExternalAnalyzer analyzer,
        UrlAlert alert)
    {
        var analyzerDirectory = Path.Combine(_analyzersFolder, analyzer.ScriptFile);
        var identity = alert.MessagingIdentity ?? new MessageIdentityV1(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            alert.DeviceInfo?.DeviceUid ?? "legacy-unknown",
            alert.TabId,
            alert.Url);

        var response = await new AnalyzerV1ProcessClient().RunAsync(
            _pythonPath,
            analyzerDirectory,
            identity,
            TimeSpan.FromSeconds(30));
        var result = System.Text.Json.JsonSerializer.Deserialize<UrlAnalysisResult>(
            response.Outcome!.Result!.Value.GetRawText(),
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        if (result is null)
            throw new InvalidOperationException("Analyzer result payload is invalid.");
        result.IsFromCache = false;
        return result;
    }

    private async Task<UrlAnalysisResult?> RunPythonAnalyzerAsync(ExternalAnalyzer analyzer, string url)
    {
        // Build path to analyzer directory and analyze.py script
        var analyzerDirectory = Path.Combine(_analyzersFolder, analyzer.ScriptFile);
        var scriptPath = Path.Combine(analyzerDirectory, "analyze.py");

        if (!Directory.Exists(analyzerDirectory))
        {
            _logger.LogError($"Analyzer directory not found: {analyzerDirectory}");
            throw new DirectoryNotFoundException($"Analyzer directory not found: {analyzerDirectory}");
        }

        if (!File.Exists(scriptPath))
        {
            _logger.LogError($"analyze.py not found in: {analyzerDirectory}");
            throw new FileNotFoundException($"analyze.py not found in: {analyzerDirectory}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _pythonPath,
            Arguments = $"\"{scriptPath}\" \"{url}\" --json",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = analyzerDirectory  // Set working directory to analyzer folder
        };

        _logger.LogDebug($"Executing: {startInfo.FileName} {startInfo.Arguments}");
        _logger.LogDebug($"Working directory: {startInfo.WorkingDirectory}");

        using var process = new Process { StartInfo = startInfo };

        string output = "";
        string errors = "";
        int exitCode = -1;

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to start Python process: {_pythonPath}");
            throw new Exception($"Failed to start Python process: {ex.Message}");
        }

        var outputTask = Task.Run(() =>
        {
            try
            {
                output = process.StandardOutput.ReadToEnd();
                errors = process.StandardError.ReadToEnd();
                process.WaitForExit();
                exitCode = process.HasExited ? process.ExitCode : -1;
            }
            catch (InvalidOperationException)
            {
                // Process was killed or disposed
                exitCode = -1;
            }
        });

        // Set timeout to 30 seconds
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
        var completedTask = await Task.WhenAny(outputTask, timeoutTask);

        if (completedTask == timeoutTask)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (InvalidOperationException) { /* Process already exited */ }
            catch (Exception ex) { _logger.LogDebug($"Error killing process: {ex.Message}"); }

            _logger.LogError($"Python analyzer {analyzer.ScriptFile} timed out after 30 seconds");
            throw new TimeoutException($"Analyzer {analyzer.ScriptFile} timed out");
        }

        await outputTask;
        var analysisJson = output;
        var analysisErrors = errors;

        if (!string.IsNullOrEmpty(analysisErrors))
        {
            _logger.LogWarning($"Python stderr: {analysisErrors}");
        }

        // COMMENTED OUT: Exit code check - Python script doesn't always return 0 even on success
        // if (exitCode != 0)
        // {
        //     _logger.LogError($"Python script exited with code {exitCode}. Stderr: {analysisErrors}");
        //     throw new Exception($"Python script failed with exit code {exitCode}: {analysisErrors}");
        // }

        if (string.IsNullOrWhiteSpace(analysisJson))
        {
            _logger.LogError("Python script returned empty output");
            throw new Exception("Python script returned empty output");
        }

        _logger.LogDebug($"Python output: {analysisJson}");

        try
        {
            var result = JsonConvert.DeserializeObject<UrlAnalysisResult>(analysisJson);
            result.IsFromCache = false; // Mark as not from cache since it's fresh from Python analyzer
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, $"Failed to deserialize JSON output: {analysisJson}");
            throw new Exception($"Failed to deserialize analyzer output: {ex.Message}");
        }
    }

    /// <summary>
    /// Check URL and domain against known phishing database
    /// </summary>
    private async Task<PhishingCheckResultVm> CheckKnownPhishingAsync(string url)
    {
        try
        {
            var domain = Common.Entities.KnownPhishingWebsite.GetDomainFromUrl(url);

            // Check if exact URL is in database
            using var scope = _serviceScopeFactory?.CreateScope();
            var phishingRepo = scope?.ServiceProvider.GetRequiredService<IKnownPhishingWebsiteRepository>() ?? _directPhishingRepo!;
            var isKnownPhishing = await phishingRepo.IsPhishingUrlAsync(url);

            // Check if domain is in database
            var isKnownPhishingDomain = await phishingRepo.IsPhishingDomainAsync(domain);

            // Get count and source info if domain is phishing
            PhishingCheckResultSource? source = PhishingCheckResultSource.Unknown;
            int matchCount = 0;

            if (isKnownPhishingDomain)
            {

                var phishingUrls = await phishingRepo.GetByDomainAsync(domain);
                var phishingUrlsList = phishingUrls.ToList();
                matchCount = phishingUrlsList.Count;

                // Get source from first match
                if (phishingUrlsList.Any())
                {
                    //source = phishingUrlsList.First().Source;
                    source = PhishingCheckResultSource.KnownList;
                }
            }

            _logger.LogDebug($"Phishing check for {url}: URL={isKnownPhishing}, Domain={isKnownPhishingDomain}, Matches={matchCount}");

            return new PhishingCheckResultVm
            {
                Is_known_phishing = isKnownPhishing,
                Is_known_phishing_domain = isKnownPhishingDomain,
                Source = source,
                Match_count = matchCount,
                Checked_at = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking phishing database for URL: {url}");

            // Return negative result on error (don't block analysis)
            return new PhishingCheckResultVm
            {
                Is_known_phishing = false,
                Is_known_phishing_domain = false,
                Source = null,
                Match_count = 0,
                Checked_at = DateTime.UtcNow
            };
        }
    }

    private Severity GetSeverityFromRiskScore(float val)
    {


        Severity severity = Severity.Unknown;
        if (val >= _severityScoreThresholdCritical)
            severity = Severity.Critical;  // risk score >= 80 = very dangerous
        else if (val >= _severityScoreThresholdHigh)
            severity = Severity.High;      // risk score >= 61 = dangerous
        else if (val >= _severityScoreThresholdMedium)
            severity = Severity.Medium;    // risk score >= 31 = moderate ris
        else
            severity = Severity.Low;       // risk score < 31 = low risk    

        return severity;
    }

    private Severity CalculateSeverity(PhishingCheckResultVm phishingCheckResult, float maxRiskScore, bool isScam)
    {
        var severity = Severity.Low;
        if (isScam)
        {
            severity = Severity.High;
            _logger.LogWarning($"⚠️ Scam indicators detected in analysis results");
        }
        else if (phishingCheckResult.Is_known_phishing_domain && phishingCheckResult.Match_count > 0)
        {
            severity = Severity.High;
            _logger.LogWarning($"⚠️ Known phishing domain detected: {phishingCheckResult.Source} ({phishingCheckResult.Match_count} URLs)");
        }
        else
        {
            _logger.LogInformation($"Max risk score from analyzers: {maxRiskScore}");
            severity = this.GetSeverityFromRiskScore(maxRiskScore);
        }

        return severity;
    }

    private void LoadConfiguration(IConfiguration configuration)
    {
        this._configuration = configuration;
        _logger.LogInformation("Reloading configuration due to SystemConfigurationChanged event");
        // In this implementation, we read configuration values directly in methods, so no need to reload into fields
        // If we had cached configuration values in fields, we would update them here
        // Get Python path from configuration
        _pythonPath = configuration.GetValue<string>("Python:ExecutablePath", "python");

        // Get analyzers folder path (absolute path from configuration)
        _analyzersFolder = configuration.GetValue<string>("Python:AnalyzersFolderPath",
            Path.Combine(Directory.GetCurrentDirectory(), "Analyzers"));

        _riskThreshold = _configuration.GetValue<int>("TrackUrl:RiskThresholdToEnableTracking", 40);
        _trackingDurationMinutes = _configuration.GetValue<int>("TrackUrl:TrackingDurationMinutes", 3000);

        _severityScoreThresholdCritical = _configuration.GetValue<float>("Analysis:SeverityScoreThresholdCritical", 80);
        _severityScoreThresholdHigh = _configuration.GetValue<float>("Analysis:SeverityScoreThresholdHigh", 80);
        _severityScoreThresholdMedium = _configuration.GetValue<float>("Analysis:SeverityScoreThresholdMedium", 80);
    }
    public Task Handle(IDomainEvent evt)
    {
        if (evt is SystemConfigurationChanged sysConfigChanged)
        {
            this.LoadConfiguration(sysConfigChanged.NewConfiguration);
        }
        return Task.CompletedTask;
    }

    public Type[] GetHandleableEvents()
    {
        return [typeof(SystemConfigurationChanged)];
    }

    /// <summary>
    /// Detects new WebsiteCategory from UrlAnalysisResult and creates it if not exists.
    /// JIRA: SCRUM-823
    /// Note: Full implementation pending - for now just logging
    /// TODO: Add actual DB persistence via IWebsiteCategoryRepository
    /// </summary>
    private Task DetectAndCreateNewWebsiteCategoryAsync(UrlAnalysisResult result)
    {
        try
        {
            if (result?.Purpose?.CategoryName == null)
            {
                return Task.CompletedTask;
            }

            var categoryName = result.Purpose.CategoryName;
            var source = "basic-url-analyzer";

            _logger.LogInformation(
                "SCRUM-823: Detected WebsiteCategory from URL analysis. " +
                "Category: {CategoryName}, URL: {Url}",
                categoryName, result.Url);

            // TODO: Implement actual category creation via IWebsiteCategoryRepository
            // The service layer should handle DB persistence
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SCRUM-823: Failed to process WebsiteCategory detection");
        }
        
        return Task.CompletedTask;
    }
}
