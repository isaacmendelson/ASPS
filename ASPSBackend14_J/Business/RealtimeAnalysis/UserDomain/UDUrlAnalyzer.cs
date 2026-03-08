using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Business.RealtimeAnalysis.Indicators;
using Business.Views;
using Common.Enums;
using Common.Models;
using Common.Models.Alerts;
using Interface.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Business.RealtimeAnalysis.UserDomain;

public class UDUrlAnalyzer : ISpecificAnalyzer
{
    private readonly ILogger<UDUrlAnalyzer> _logger;
    private readonly IConfiguration _configuration;
    private readonly IKnownPhishingWebsiteRepository _phishingRepo;
    private readonly ASView _asView;
    private readonly string _pythonPath;
    private readonly string _analyzersFolder;

    public ExternalAnalyzer[] ExternalAnalyzers { get; }

    public UDUrlAnalyzer(
        ILogger<UDUrlAnalyzer> logger,
        IConfiguration configuration,
        IKnownPhishingWebsiteRepository phishingRepo,
        ASView asView)
    {
        _logger = logger;
        _configuration = configuration;
        _phishingRepo = phishingRepo;
        _asView = asView;
        
        // Get Python path from configuration
        _pythonPath = _configuration.GetValue<string>("Python:ExecutablePath", "python");
        
        // Get analyzers folder path (absolute path from configuration)
        _analyzersFolder = _configuration.GetValue<string>("Python:AnalyzersFolderPath", 
            Path.Combine(Directory.GetCurrentDirectory(), "Analyzers"));

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

        _logger.LogInformation($"Analyzing URL: {urlAlert.Url}");

        // STEP 0: Check if domain is whitelisted (SafeDomains)
        var alertDomain = Common.Entities.KnownPhishingWebsite.GetDomainFromUrl(urlAlert.Url);
        if (!string.IsNullOrEmpty(alertDomain) && _asView.IsSafeDomain(alertDomain))
        {
            _logger.LogInformation($"Domain '{alertDomain}' is whitelisted (SafeDomains). Skipping analysis.");

            var whitelistedResult = new UrlAnalysisResultVm
            {
                Url = urlAlert.Url,
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
                risk_assessment = new RiskAssessment(100, "Whitelisted", false, 1),
                website_category = null,
                Reputation = null,
                Warnings = Array.Empty<string>(),
                missing_data = Array.Empty<string>()
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
                    ["url"] = urlAlert.Url,
                    ["analyzers_run"] = 0,
                    ["analyzers_total"] = ExternalAnalyzers.Length,
                    ["is_whitelisted"] = true
                }
            );
        }

        // STEP 1: Check against known phishing database FIRST (fast check)
        var phishingCheckResult = await CheckKnownPhishingAsync(urlAlert.Url);
        
        // If it's a known phishing URL, we can return immediately with Critical severity
        if (phishingCheckResult.Is_known_phishing)
        {
            _logger.LogWarning($"⚠️ KNOWN PHISHING URL DETECTED: {urlAlert.Url} (Source: {phishingCheckResult.Source})");
            
            // Create indicator
            var phishingIndicator = new KnownPhishingIndicator(
                urlAlert.Url,
                Common.Entities.KnownPhishingWebsite.GetDomainFromUrl(urlAlert.Url), 
                true,
                true,
                phishingCheckResult.Source,
                phishingCheckResult.Match_count,
                new BooleanScore(true, 1.0f, true),
                AnalysisLevel.Device,
                1,
                1.0f
            );
            
            var criticalIndicators = new List<IIndicator> { phishingIndicator };
            
            //return new AnalyzerResult(
            //    Severity.Critical,
            //    "⚠️ KNOWN PHISHING URL DETECTED!",
            //    criticalIndicators,
            //    new Dictionary<string, object>
            //    {
            //        ["is_known_phishing"] = true,
            //        ["phishing_source"] = phishingCheckResult.source ?? "Unknown",
            //        ["url"] = urlAlert.Url,
            //        ["domain"] = phishingCheckResult.source ?? "",
            //        ["match_count"] = phishingCheckResult.match_count,
            //        ["risk_score"] = 100
            //    }
            //);
        }

       

        // STEP 2: Continue with normal external analyzers
        var results = new List<UrlAnalysisResultVm>();
        var errors = new List<string>();

        // Execute each external analyzer
        foreach (var analyzer in ExternalAnalyzers.OrderBy(a => a.Order))
        {
            try
            {
                var result = new UrlAnalysisResultVm();
                _logger.LogInformation($"Running analyzer: {analyzer.ScriptFile} (Order: {analyzer.Order}, Weight: {analyzer.Weight})");

                // STEP 2: Check if a UrlAnalysisResult exists for this URL (cache)
                var cacheEnabled = configuration.GetValue<bool>("Analysis:CacheEnabled", true);
                if (cacheEnabled && this._asView.TryGetCachedUrlAnalysis(urlAlert.Url, 0, out var cachedResult) && cachedResult is not null)
                {
                    _logger.LogInformation($"Cache hit for URL: {urlAlert.Url}");
                    cachedResult.IsFromCache = true;

                    // Add phishing check result to cached result
                    cachedResult.AnalysisResult.phishing_check = phishingCheckResult;

                    result = new UrlAnalysisResultVm()
                    {
                        Url = urlAlert.Url,
                        Domain = Common.Entities.KnownPhishingWebsite.GetDomainFromUrl(urlAlert.Url),
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
                        Warnings = cachedResult.AnalysisResult.Warnings
                    };
                }
                else
                {
                    result = await RunPythonAnalyzerAsync(analyzer, urlAlert.Url);
                }

                if (result != null)
                {
                    result.phishing_check = phishingCheckResult;
                    results.Add(result);
                    _logger.LogInformation($"Analyzer {analyzer.ScriptFile} completed successfully. Risk Score: {result.risk_assessment?.risk_score ?? 0}");
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

        // STEP 3: Add phishing check to results (if we got Python analyzer results)
        if (results.Any())
        {
            var firstResult = results.First();
            firstResult.phishing_check = phishingCheckResult;
        }

        // STEP 4: Determine overall severity based on results
        var severity = Severity.Low;
        
        // Elevate severity if phishing domain detected (even if not exact URL match)
        if (phishingCheckResult.Is_known_phishing_domain && phishingCheckResult.Match_count > 0)
        {
            severity = Severity.High;
            _logger.LogWarning($"⚠️ Known phishing domain detected: {phishingCheckResult.Source} ({phishingCheckResult.Match_count} URLs)");
        }
        
        if (results.Any())
        {
            var maxRiskScore = results
                .Where(r => r.risk_assessment != null)
                .Max(r => r.risk_assessment!.risk_score);
            
            var hasScam = results
                .Where(r => r.risk_assessment != null)
                .Any(r => r.risk_assessment!.is_scam);

            if (hasScam || maxRiskScore <= 30)
                severity = Severity.Critical;  // safety score <= 30 = very dangerous
            else if (maxRiskScore <= 50)
                severity = Severity.High;      // safety score <= 50 = dangerous
            else if (maxRiskScore <= 70)
                severity = Severity.Medium;    // safety score <= 70 = moderate risk
        }

        // STEP 5: Create indicators list
        var indicators = new List<IIndicator>();
        
        // Add phishing indicator if domain is suspicious
        if (phishingCheckResult.Is_known_phishing_domain)
        {
            var domain = Common.Entities.KnownPhishingWebsite.GetDomainFromUrl(urlAlert.Url);
            var phishingIndicator = new KnownPhishingIndicator(
                urlAlert.Url,
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
            if (indicators.Any(i =>  i is KnownPhishingIndicator knownPhishingIndicator))
            {
                string msg =  $"Known phishing URL detected: ";

                var action = new ProtectiveAction(ProtectiveActionSubject.Device, ProtectiveActionType.UserDisplayNotification,AnalysisLevel.Device, msg, alert.AlertId);
                protectiveActions.Add(action);
            }
        }
        // Aggregate results
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
                ["url"] = urlAlert.Url,
                ["analyzers_run"] = results.Count,
                ["analyzers_total"] = ExternalAnalyzers.Length
            }
        );

        // Add aggregate risk assessment if we have results
        if (results.Any())
        {
            var avgRiskScore = results
                .Where(r => r.risk_assessment != null)
                .Average(r => r.risk_assessment!.risk_score);
            
            var isScam = results
                .Where(r => r.risk_assessment != null)
                .Any(r => r.risk_assessment!.is_scam);

            analyzerResult.Details["aggregate_risk_score"] = avgRiskScore;
            analyzerResult.Details["is_scam"] = isScam;
        }

        return analyzerResult;
    }

    private async Task<UrlAnalysisResultVm?> RunPythonAnalyzerAsync(ExternalAnalyzer analyzer, string url)
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
            var result = JsonConvert.DeserializeObject<UrlAnalysisResultVm>(analysisJson);
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
            var isKnownPhishing = await _phishingRepo.IsPhishingUrlAsync(url);
            
            // Check if domain is in database
            var isKnownPhishingDomain = await _phishingRepo.IsPhishingDomainAsync(domain);

            // Get count and source info if domain is phishing
            PhishingCheckResultSource? source = PhishingCheckResultSource.Unknown;
            int matchCount = 0;
            
            if (isKnownPhishingDomain)
            {

                var phishingUrls = await _phishingRepo.GetByDomainAsync(domain);
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
}
