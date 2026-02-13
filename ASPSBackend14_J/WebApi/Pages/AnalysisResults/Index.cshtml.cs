using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Queries;
using Common.Entities;
using Common.Models;

namespace WebApi.Pages.AnalysisResults
{
    public class AnalysisResultWithInfo
    {
        public AnalysisResultContainer AnalysisResult { get; set; } = null!;
        public string? UserName { get; set; }
        public DeviceAlertEntity? Alert { get; set; }
        public UserDevice? Device { get; set; }
    }

    public class IndexModel : PageModel
    {
        private readonly CQRSClient _cqrsClient;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(CQRSClient cqrsClient, ILogger<IndexModel> logger)
        {
            _cqrsClient = cqrsClient;
            _logger = logger;
        }

        public List<AnalysisResultWithInfo> ResultsWithInfo { get; set; } = new();
        public int TimeFilter { get; set; } = 24;
        public string? ErrorMessage { get; set; }
        public string? InfoMessage { get; set; }

        [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        public async Task OnGetAsync(int? hours)
        {
            TimeFilter = hours ?? 24;

            try
            {
                _logger.LogInformation("Loading analysis results via CQRS (last {Hours} hours)", TimeFilter);

                var query = new GetAllAnalysisResultsQuery { Hours = TimeFilter, Search = Search };
                var result = await _cqrsClient.SendQueryAsync<GetAllAnalysisResultsQueryResult>(query);

                if (result.Success)
                {
                    if (!result.AnalysisResults.Any())
                    {
                        InfoMessage = $"Query succeeded but returned 0 analysis results for the last {TimeFilter} hours. This may be normal if no device alerts have been analyzed recently.";
                        _logger.LogInformation("No analysis results found for last {Hours} hours", TimeFilter);
                    }
                    // Load related data for each analysis result
                    foreach (var analysisResult in result.AnalysisResults)
                    {
                        var resultInfo = new AnalysisResultWithInfo { AnalysisResult = analysisResult };

                        // Get user name
                        if (!string.IsNullOrEmpty(analysisResult.UserKeyField))
                        {
                            try
                            {
                                var userQuery = new GetUserByKeyQuery
                                {
                                    UserKey = new Key("User", analysisResult.UserKeyField)
                                };
                                var userResult = await _cqrsClient.SendQueryAsync<GetUserByKeyQueryResult>(userQuery);
                                if (userResult.Success && userResult.User != null)
                                {
                                    resultInfo.UserName = $"{userResult.User.FirstName} {userResult.User.LastName}";
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to load user for analysis result");
                            }
                        }

                        // Get device alert
                        if (!string.IsNullOrEmpty(analysisResult.DeviceAlertKeyField))
                        {
                            try
                            {
                                var alertQuery = new GetAlertByKeyQuery
                                {
                                    AlertKey = new Key("DeviceAlert", analysisResult.DeviceAlertKeyField)
                                };
                                var alertResult = await _cqrsClient.SendQueryAsync<GetAlertByKeyQueryResult>(alertQuery);
                                if (alertResult.Success && alertResult.Alert != null)
                                {
                                    resultInfo.Alert = alertResult.Alert;

                                    // Get device from alert's DeviceKeyField or DeviceUid
                                    try
                                    {
                                        if (!string.IsNullOrEmpty(resultInfo.Alert.DeviceKeyField))
                                        {
                                            var deviceQuery = new GetDeviceByKeyQuery
                                            {
                                                DeviceKey = new Key("UserDevice", resultInfo.Alert.DeviceKeyField)
                                            };
                                            var deviceResult = await _cqrsClient.SendQueryAsync<GetDeviceByKeyQueryResult>(deviceQuery);
                                            if (deviceResult.Success && deviceResult.Device != null)
                                            {
                                                resultInfo.Device = deviceResult.Device;
                                            }
                                        }
                                        else if (!string.IsNullOrEmpty(resultInfo.Alert.DeviceUid))
                                        {
                                            var deviceQuery = new GetDeviceByUidQuery
                                            {
                                                DeviceUid = resultInfo.Alert.DeviceUid
                                            };
                                            var deviceResult = await _cqrsClient.SendQueryAsync<GetDeviceByUidQueryResult>(deviceQuery);
                                            if (deviceResult.Success && deviceResult.Device != null)
                                            {
                                                resultInfo.Device = deviceResult.Device;
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Failed to load device for analysis result");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to load alert for analysis result");
                            }
                        }

                        ResultsWithInfo.Add(resultInfo);
                    }

                    _logger.LogInformation("Analysis results loaded: {Count}", ResultsWithInfo.Count);
                }
                else
                {
                    ErrorMessage = result.Message;
                    _logger.LogError("Failed to load analysis results: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading analysis results");
                ErrorMessage = $"Error loading analysis results: {ex.Message}";
            }
        }
    }
}
