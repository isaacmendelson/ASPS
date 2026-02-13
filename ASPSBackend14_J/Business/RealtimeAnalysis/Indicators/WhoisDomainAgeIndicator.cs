#nullable enable

using Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis.Indicators
{
    [Serializable]
    public class WhoIsDomainAgeIndicator : Indicator
    {
        private DateTime domainRegistrationDate = DateTime.UtcNow;
        protected WhoIsDomainAgeIndicator() { }

        public WhoIsDomainAgeIndicator(AnalysisLevel layer, NumericScore score, int? sequence, float? weight = 1)
            : base(score, layer, sequence ?? 0, weight ?? 1)
        {
        }

        public WhoIsDomainAgeIndicator(int value, NumericScore score, AnalysisLevel layer, int? sequence, float? weight = 1)
            : base(score, layer, sequence ?? 0, weight ?? 1)
        {
            this.domainRegistrationDate = DateTime.UtcNow.Subtract(new TimeSpan(value, 0, 0, 0));
            this.Value = value;
        }

        public override IndicatorType IndicatorType { get => IndicatorType.WhoIsDomainAge; }
        public override IndicatorSource Source { get => IndicatorSource.Domain; }

        //public NumericScore? Score { get; set; }
       // public override object? Value { get; private set; }
        public int TypedValue { get; private set; }

        public override object Value
        {
            get
            {
                return (DateTime.UtcNow - this.domainRegistrationDate).TotalDays;
            }

        }
        //public void SetScore(NumericScore score)
        //{
        //    this.Score = score;
        //}

        public void SetValue(int value, float confidence)
        {
            this.domainRegistrationDate = DateTime.UtcNow.Subtract(new TimeSpan(value, 0, 0, 0));
            base.SetValue(value, confidence);
            this.TypedValue = value;
        }
    }
}
