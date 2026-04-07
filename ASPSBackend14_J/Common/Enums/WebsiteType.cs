using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums
{
    [Obsolete("Use WebsiteCategoryViews from ASView instead")]
    public enum WebsiteType
    {
        Unknown = 0,
        Analytics = 1,
        Banking = 2,
        News = 3,
        ECommerce = 4,
        Telecom = 5,
        Dating = 6,
        Exchange = 7,
        Healthcare = 8,
    }
}
