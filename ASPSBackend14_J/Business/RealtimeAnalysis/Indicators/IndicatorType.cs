using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis.Indicators
{
    public enum IndicatorType
    {
        WhoIsDomainAge = 1,
        NoMxRecord = 2,
        NameServers = 3,
        DomainBlacklisted = 4,
        ContentKnownWebsiteTemplate = 5,
        CertificateAge = 6,
        CertificateIssuer = 7,
        CertificateInvalid = 8,
        CertificateExpired = 9,
        ContentWebsiteType = 10,
        IPBlacklisted = 11,
        ContentFalsePromise = 12,
        ContentMagicWords = 13,
        ContentTimePress = 14,
        RiskPhising = 15,
        RiskCloaking = 16,
        PhishShield = 17,   // PhishShield score
        UserIsTargeted = 18,    // User info is in darnet sales leads lists
        NegativeUserReviews = 19,
        WhoIsCountry = 20,
        WhoIsPrivacyProtected = 21,
        WhoIs = 21,
        ContentAnalysis = 22,
        KnownPhishing = 23,  // Known phishing URL/domain detection
        RemoteAccess = 24,  // Remote access tool detected


    }
}
