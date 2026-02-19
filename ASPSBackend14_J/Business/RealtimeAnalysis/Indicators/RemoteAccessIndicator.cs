using Business.RealtimeAnalysis.UserDomain;
using Common.Enums;
using Common.Models.Alerts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis.Indicators
{
    public class RemoteAccessIndicator : Indicator
    {

        public RemoteAccessIndicator() { }
        public RemoteAccessIndicator(RemoteAccessApp remoteAccessApp, int runningProcesses, string? connectionUrl, 
            ConnectionStatus connectionStatus, int connectionsCount, SessionStatus sessionStatus, BrowserTab[]? browserTabs, 
            NumericScore score, AnalysisLevel level, int? sequence, float? weight = 1)
           : base(score, level, sequence ?? 0, weight ?? 1)
        {
            this.RemoteAccessApp = remoteAccessApp;
            this.RunningProcesses = runningProcesses;
            this.ConnectionUrl = connectionUrl;
            this.ConnectionStatus = connectionStatus;
            this.ConnectionsCount = connectionsCount;
            this.SessionStatus = sessionStatus;
            this.BrowserTabs = browserTabs;

            //this.TypedValue = value;
            //this.Score = score;
        }
        public override IndicatorSubject IndicatorSubject
        {
            get 
            {
                return IndicatorSubject.Device;
            }
        }
        public RemoteAccessApp RemoteAccessApp { get; set; }
        public int RunningProcesses { get; set; }
        public string? ConnectionUrl { get; set; } = string.Empty;
        public ConnectionStatus ConnectionStatus { get; set; }
        public int ConnectionsCount { get; set; }
        public SessionStatus SessionStatus { get; set; }
        public BrowserTab[]? BrowserTabs { get; set; }

        //public NumericScore Score { get; set; }

        public override IndicatorType IndicatorType { get => IndicatorType.ContentAnalysis; }

        public override IndicatorSource Source { get => IndicatorSource.Domain; }

        //public void SetScore(NumericScore score)
        //{
        //    this.Score = score;
        //}

        //public ContentAnalysis TypedValue { get; private set; }
        public void SetValue(ContentAnalysis value, float confidence)
        {
            base.SetValue(value, confidence);
            //this.TypedValue = value;
        }
    }
}
