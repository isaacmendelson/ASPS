using Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis.Indicators
{
    public class UserIsTargetedIndicator : Indicator
    {
        public UserIsTargetedIndicator() { }

        public BooleanScore Score { get; set; }

        public override IndicatorType IndicatorType { get => IndicatorType.NoMxRecord; }

        public override IndicatorSource Source { get => IndicatorSource.Darknet; }

        public void SetScore(BooleanScore score)
        {
            this.Score = score;
        }
    }
}
