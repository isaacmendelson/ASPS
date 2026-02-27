using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.UserDomain;
using Common.Enums;
using Common.Models.Alerts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis
{
    public class IndicatorFactory : IIndicatorFactory
    {
        public IndicatorFactory()
        {
        }

        public IIndicator[] CreateIndicators(AnalysisResult analysisResult)
        {
            var res = new List<Indicator>();
            switch(analysisResult)
            {
                case UrlAnalysisResultVm vm:

                    var score = new BooleanScore(vm.phishing_check?.Is_known_phishing == true, 1.0f, true);
                    if (vm.phishing_check?.Is_known_phishing == true)
                    {
                        var domain = Common.Entities.KnownPhishingWebsite.GetDomainFromUrl(vm.Url);
                        var phishingIndicator = new KnownPhishingIndicator(
                            
                            vm.Url,
                            domain,
                            vm.phishing_check.Is_known_phishing,
                            vm.phishing_check.Is_known_phishing_domain,
                            vm.phishing_check.Source,
                            vm.phishing_check.Match_count,
                            score,
                            AnalysisLevel.Device,
                            1,
                            1.0f
                        );
                        res.Add(phishingIndicator);
                        return res.ToArray();
                    }

                    if (vm.Whois?.Success == true)
                    {
                        var whoIsIndicator = new WhoIsIndicator(vm.Whois, new NumericScore(vm.Whois.risk_score, 1, true), AnalysisLevel.Device,1, 1);
                        res.Add(whoIsIndicator);

                        var whoIsDomainAgeIndicator = new WhoIsDomainAgeIndicator(vm.Whois.domain_age_days, GetScoreFromDomainAge(vm.Whois.domain_age_days), AnalysisLevel.Device, 1, 1);
                        res.Add(whoIsDomainAgeIndicator);

                        var whoisIsPrivacyProtectedIndicator = new WhoisIsPrivacyProtectedIndicator(vm.Whois.privacy_protected, GetScoreForPrivacyProtectedWhoisInfo(vm.Whois.privacy_protected), AnalysisLevel.Device, 1, 1);
                        res.Add(whoisIsPrivacyProtectedIndicator);

                        if (vm.Whois.country is not null)
                        {
                            var whoisCountryIndicator = new WhoisCountryIndicator(vm.Whois.country, new NumericScore(0, 1, true), AnalysisLevel.Device, 1, 1);
                            res.Add(whoisCountryIndicator);
                        }
                    }

                    if (vm.Purpose?.Category != WebsiteType.Unknown)
                    {
                        var websiteTypeIndicator = new WebsiteTypeIndicator(vm.Purpose?.Category??0, new NumericScore(0, vm.Purpose is null ? 0 :vm.Purpose.Confidence, true), AnalysisLevel.Device, 1, 1.0f);
                        res.Add(websiteTypeIndicator);
                    }
                    if (vm.content_analysis?.Success == true && vm.content_analysis?.HasSuspiciousKeywords == true)
                    {
                        var contentAnalysisScore = new NumericScore(1, 1, true);
                        var contentAnalysisIndicator = new ContentAnalysisIndicator(new ContentAnalysis(vm.content_analysis), contentAnalysisScore, AnalysisLevel.Device, 1);
                        res.Add(contentAnalysisIndicator);

                    }
                    if (vm.ml_analysis?.Success== true)
                    {
                        var x = vm.ml_analysis;
                        var mlAnalysisIndicator = new MlAnalysisIndicator(vm.ml_analysis, AnalysisLevel.Device, 1);
                    }

                    return res.ToArray();

                case RemoteAccessAnalysisResultVm vm:

                    if (!vm.Success)
                    {
                        break;
                    }
                    if (vm.SessionStatus == (int)SessionStatus.Open)
                    {
                        var nScore = new NumericScore(0, 0, false);
                        BrowserTab[]? browserTabs = null;

                        if (vm.risk_assessment?.risk_score is not null)
                        {
                            nScore = new NumericScore(vm.risk_assessment.risk_score, vm.risk_assessment.confidence, true);
                        }
                        var remoteAccessSessionOpenIndicator = new RemoteAccessIndicator(vm.RemoteAccessApp, vm.RunningProcesses,
                            vm.ConnectionUrl, vm.ConnectionStatus, vm.ConnectionsCount, (SessionStatus)vm.SessionStatus, browserTabs, nScore, AnalysisLevel.Device,
                            0, 1);
                    }
                    break;
            }
            return Array.Empty<IIndicator>();
        }

         private NumericScore GetScoreFromDomainAge(int domainAgeDays)
        {
            var val = 0f;
            if (domainAgeDays < 30)
                val =  0.0f; // Very High Risk
            else if (domainAgeDays < 180)
                val = 0.3f; // High Risk
            else if (domainAgeDays < 365)
                val = 0.6f; // Medium Risk
            else
                val = 1.0f; // Low Risk
            return new NumericScore(val, 1, true);
        }

        private BooleanScore GetScoreForPrivacyProtectedWhoisInfo(bool isPrivacyProtected)
        {
            float val = 0f;
            if (isPrivacyProtected)
            {
                val = 0.1f;
            }

            return new BooleanScore(isPrivacyProtected, 1 , true);
        }

    }
}
