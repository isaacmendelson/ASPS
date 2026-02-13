using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis.Indicators
{
    public enum IndicatorSource
    {
        Unknown = 0,
        Domain = 1,
        Content = 2,
        Certificate = 3,
        Risk = 4,
        External = 5,
        Darknet = 6,
        Blacklist = 7,
        Database = 8  // Database-sourced indicators
    }
}
