using Business.RealtimeAnalysis.UserDomain;
using Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis.Indicators
{
    [Serializable]
    [DataContract]
    public class WhoIsIndicator : Indicator
    {
        public WhoIsIndicator() { }
        public WhoIsIndicator(WhoisVm value, NumericScore score, AnalysisLevel layer, int? sequence, float? weight = 1)
           : base(score, layer, sequence ?? 0, weight ?? 1)
        {
            this.Value = new Whois(value);
            this.TypedValue = new Whois(value);
            this.Score = score;
        }

        public Whois Value { get; set; }
        public NumericScore Score { get; set; }

        public override IndicatorType IndicatorType { get => IndicatorType.WhoIs; }

        public override IndicatorSource Source { get => IndicatorSource.Domain; }

        public void SetScore(NumericScore score)
        {
            this.Score = score;
        }

        public Whois TypedValue { get; private set; }
        public void SetValue(Whois value, float confidence)
        {
            base.SetValue(value, confidence);
            this.TypedValue = value;
        }
    }
}
