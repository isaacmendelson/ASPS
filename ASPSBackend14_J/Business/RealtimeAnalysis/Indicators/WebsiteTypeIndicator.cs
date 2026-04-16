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
    /// <summary>
    /// Indicator for website category classification.
    /// JIRA: SCRUM-821 - Updated to use string category names instead of WebsiteType enum.
    /// </summary>
    public class WebsiteTypeIndicator : Indicator
    {
        protected WebsiteTypeIndicator() { }

        public WebsiteTypeIndicator(string categoryName, NumericScore score, AnalysisLevel layer, int? sequence, float? weight = 1)
            : base(score, layer, sequence ?? 0, weight ?? 1)
        {
            this.CategoryName = categoryName ?? "unknown";
        }

        /// <summary>
        /// Website category name (e.g., "banking", "ecommerce").
        /// Use ASView.GetCategoryView(CategoryName) to get full category details.
        /// </summary>
        public string CategoryName { get; private set; }

        public string TypedValue { get; private set; }

        public void SetValue(string value, float confidence)
        {
            base.SetValue(value, confidence);
            this.TypedValue = CategoryName;
        }
    }
}
