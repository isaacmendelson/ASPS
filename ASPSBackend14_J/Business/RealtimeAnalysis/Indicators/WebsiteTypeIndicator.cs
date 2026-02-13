//using Akka.Util;
using Common.Enums;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;

using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis.Indicators
{
    public class WebsiteTypeIndicator : Indicator
    {
        protected WebsiteTypeIndicator() { }

        public WebsiteTypeIndicator(WebsiteType websiteType, NumericScore score, AnalysisLevel layer, int? sequence, float? weight = 1)
            : base(score, layer, sequence ?? 0, weight ?? 1)
        {
            this.WebsiteType = websiteType;
        }

        public WebsiteType WebsiteType { get; private set; }

        public WebsiteType TypedValue { get; private set; }

        public void SetValue(WebsiteType value, float confidence)
        {
            base.SetValue(value, confidence);
            this.TypedValue = WebsiteType;
        }
    }
}
