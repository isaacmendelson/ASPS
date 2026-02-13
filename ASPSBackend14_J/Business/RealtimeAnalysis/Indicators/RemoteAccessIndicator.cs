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
        public RemoteAccessIndicator(RemoteAccessAlert alertData, NumericScore score, AnalysisLevel level, int? sequence, float? weight = 1)
           : base(score, level, sequence ?? 0, weight ?? 1)
        {
            this.RemoteAccessApp = alertData.RemoteAccessApp;
            this.RunningProcesses = alertData.RunningProcesses;
            this.ConnectionUrl = alertData.ConnectionUrl;
            this.ConnectionStatus = alertData.ConnectionStatus;
            this.ConnectionsCount = alertData.ConnectionsCount;


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
        public string ConnectionUrl { get; set; } = string.Empty;
        public ConnectionStatus ConnectionStatus { get; set; }
        public int ConnectionsCount { get; set; }
        public int SessionStatus { get; set; }

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
