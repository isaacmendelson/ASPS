מסמך ארכיטקטורה: שכבת המשתמש (User Layer) במערכת ASPS – גרסה מורחבת

1. סקירה כללית וחזון המערכת

שכבת המשתמש (User Layer) מהווה את רכיב ה-Orchestration והניתוח המרכזי במערכת ASPS. בניגוד לשכבת המכשיר (Device Layer), המוגבלת לניתוח אירועים בודדים (Atomic Events), שכבת המשתמש מבצעת קורלציה חוצת-מכשירים (Cross-device correlation) של נקודות טלמטריה מרוחקות לכדי תמונה הוליסטית אחת.

החזון הארכיטקטוני של השכבה הוא מימוש "Fraud Storytelling" – היכולת לזהות את רצף האירועים המרכיב את סיפור ההונאה, גם כאשר השלבים השונים מתרחשים בפלטפורמות שונות (למשל: פיתיון ב-Desktop, תקשורת ב-Mobile).

השוואה ארכיטקטונית: Device Layer vs. User Layer

| מאפיין | שכבת המכשיר (Device Layer) | שכבת המשתמש (User Layer) |
| --- | --- | --- |
| היקף ניתוח | מנתחת אירוע בודד (סינכרוני) | ניתוח רב-ממדי וקורלציה בין אירועים |
| פריסה (Scope) | מכשיר קצה בודד | זהות משתמש אחודה (Cross-Platform) |
| ציר זמן | Snapshot של זמן אמת | שילוב אירועי עבר (History) וזמן אמת |
| מקור סמכות | Device Context | Holistic User Context |
| תוצר מרכזי | Score ל-URL או למספר טלפון | FinalRiskScore מנורמל למשתמש |

- 

2. ישויות ליבה ומבנה נתונים (Entities)

2.1 ישות המשתמש (UDUser)

הישות המרכזית המחזיקה את מצב האבטחה של המשתמש בכלל המכשירים.

public class UDUser

{

// זהות וניהול מכשירים

public string UserKey { get; set; }

public List<Device> Devices { get; set; } // גישה ישירה לאובייקטי מכשיר לצורך ניתוח OpenTabs

// מצב אבטחה אחוד (Global State)

public UserRiskProfile RiskProfile { get; set; }

public int CurrentRiskScore { get; set; }

public bool IsInImmediateDanger { get; set; }

public bool IsCrossPlatformLocked { get; set; } // נעילה גורפת המופצת לכלל ה-Agents

// ניהול הונאות אקטיביות

public List<ScamInProgress> ScamsInProgress { get; set; }

public List<TrackedDomain> TrackedDomains { get; set; }

// היסטוריה מאוחדת (Aggregation)

public List<UrlVisit> UrlHistory { get; set; }

public List<PhoneCall> CallHistory { get; set; }

public List<RemoteAccessSession> RemoteAccessHistory { get; set; }

// נתונים סטטיסטיים מעובדים

public UserBehaviorStats BehaviorStats { get; set; }

}

2.2 מנתח המשתמש (UDUserAnalyzer)

רכיב ה-Logic Engine המחשב את רמות הסיכון ומקבל החלטות אופרטיביות.

מתודות ליבה (Method Signatures):

UpdateUserRisk(UDUser user): עדכון פרופיל הסיכון על בסיס נתונים חדשים.

CheckImmediateDanger(UDUser user): בדיקת תנאי סף לסכנה מיידית (Boolean).

DetectScamInProgress(UDUser user, AnalysisResult result): זיהוי תבניות הונאה.

DetermineActions(UDUser user): קביעת רשימת פעולות הגנה (Protective Actions).

למידה אדפטיבית: המערכת מעדכנת את ה-BaitConfidence באופן רציף. אם משתמש ממשיך באינטראקציה עם אתר שסומן כפיתיון (למשל, מילוי טופס פרטים), המערכת מעלה את רמת הוודאות של ה-Scam. מידע זה מוזן חזרה ל-Device Layer כעדכון ל-Blocklists המקומיים דרך אירוע SetTrackedDomains.

- 

3. מנגנון "סיפור" ההונאה (Scam Journey)

המערכת ממפה אירועים מבודדים לרצף לוגי של שלבי הונאה (ScamProgressItemType).

תרשים שלבי הונאה ומפוי Enums

[ שלב 1: פיתיון ] -> [ שלב 2: אינטראקציה ] -> [ שלב 3: הסלמה ] -> [ שלב 4: ניצול ]

|                   |                   |                   |

(Bait/Ad)       (PersonalDetails)    (RemoteAccess/Call)   (Payment/OTP)

|                   |                   |                   |

ENUM: Ad          ENUM: FormSubmit      ENUM: RemoteStarted   ENUM: PaymentAttempt

קישוריות Enums למסע:

Ad / Bait: זיהוי ראשוני של מודעת פיתיון (למשל: "Investment Opportunity").

PersonalDetailsFormSubmit: הזנת פרטים בטופס (שלב ה-Leads).

IncomingCallFakeNumber: שיחה נכנסת מ"נציג" (Social Engineering).

RemoteAccessStarted: מתן שליטה מרחוק לתוקף.

PaymentAttempt / OTP Interception: שלב גניבת הכספים המיידי.

- 

4. מנוע חישוב סיכון (Risk Scoring Engine)

ציון הסיכון הסופי מחושב בשקלול של היסטוריה, דעיכה בזמן ואיומים אקטיביים.

הנוסחה המתמטית

FinalRiskScore=min(100,⌊(BaseRisk+∑WeightedFactors)×TimeDecayFactor

days

+ActiveThreats⌋)

BaseRisk: מחושב כ- 0.3×VulnerabilityScore+0.4×ExposureScore.

VulnerabilityScore: מודד נטייה היסטורית (גלישה לאתרים מסוכנים ב-30 יום האחרונים).

ExposureScore: מודד חשיפה נוכחית (אתרים רגישים פתוחים, שיחות חשודות ב-24 שעות).

Time Decay: דעיכה מעריכית של אירועי עבר (דיפולט: 0.95 ליום). מוחל על אירועים היסטוריים לפני הוספת איומים אקטיביים.

ActiveThreats: תוספת ליניארית עבור הונאות פעילות (ScamInProgress * Confidence).

טבלת משקלים (Weights) לחישוב

| גורם (Factor) | משקל (Weight) | תיאור |
| --- | --- | --- |
| Risky URL | 1.0 | ביקור באתר בעל Score גבוה מ-50. |
| Suspicious Call | 1.5 | שיחה ממספר המזוהה כחשוד/מזוייף. |
| Inbound Remote Access | 2.0 | חיבור שליטה מרחוק נכנס (Inbound) פעיל. |
| Scam In Progress | 3.0 | זיהוי ודאי של שלב במסע הונאה אקטיבי. |

- 

5. זיהוי סכנה מיידית (Immediate Danger) ויכולות יירוט

סכנה מיידית מוגדרת כנקודת המפגש בין יכולת תקיפה (Remote Access) לנכס רגיש.

לוגיקת זיהוי סכנה (Pseudo-Code)

IF (device.RemoteAccessSessions.Any(s => s.IsActive && s.Direction == Inbound))

AND (device.OpenTabs.Any(t => t.IsSensitiveSite && t.IsUserLoggedIn))

THEN

SET User.IsInImmediateDanger = true;

SET User.CurrentRiskScore = 100;

EXECUTE ImmediateCountermeasures();

קטגוריות אתרים רגישים (Sensitive Sites Scope)

| קטגוריה | דוגמאות | סיבת הגדרה |
| --- | --- | --- |
| בנקאות/פיננסים | Bank Leumi, PayPal | העברות כספים וניהול חשבונות. |
| קריפטו | Binance, Coinbase | חשיפת ארנקים דיגיטליים. |
| ממשלתי | gov.il, רשות המיסים | גניבת זהות ומידע אישי רגיש. |
| מסחר/בורסה | eToro, Interactive Brokers | ביצוע פעולות מסחר לא מורשות. |

יכולות יירוט והגנה מתקדמות

יירוט OTP (One-Time Password): קורלציה בין קבלת SMS במכשיר הנייד לבין ניסיון הזנה ב-Browser תחת שליטה מרחוק. המערכת חוסמת את הצגת הקוד או מתריעה למשתמש.

החשכת מסך (Black Screen): טכניקת DOM Manipulation המופעלת על ידי ה-Extension. המערכת מזריקה CSS/JS המסתיר ("Redacting") אלמנטים רגישים (כמו יתרת חשבון, מספרי כרטיס אשראי) עבור הצד המרוחק, בעוד המשתמש המקומי שומר על נראות מלאה.

- 

6. פעולות הגנה ומדרגות תגובה (Protective Actions)

המערכת מפעילה "נעילה חוצת פלטפורמות" (Cross-Platform Lock) דרך מנגנון SetTrackedDomains, המסנכרן את כלל ה-Agents למצב כוננות גבוהה.

מטריצת תגובה (Response Matrix)

| טווח ציון | רמת סיכון | פעולות הגנה מופעלות |
| --- | --- | --- |
| 0-20 | נמוך | מעקב פאסיבי בלבד. |
| 21-40 | בינוני-נמוך | באנר אזהרה בדפדפן (Warning Banner). |
| 41-60 | בינוני | הודעת Push, התראה מודאלית, הפעלת מעקב מפורט (Detailed Tracking). |
| 61-80 | גבוה | חסימת הדף, ניתוק Remote Access, התראת SMS לאיש קשר חירום. |
| 81-100 | קריטי | Cross-Platform Lock, החשכת מסך (Black Screen), נעילת דפדפן מלאה. |

- 

7. נספחים והגדרות מערכת

7.1 הגדרות מערכת (UserLayerSettings)

AggregationPeriodDays: 30 יום (טווח הניתוח ההיסטורי).

TimeDecayFactor: 0.95 (דעיכה יומית של ציון הסיכון).

NormalizationCap: 100 (תקרת ציון סיכון מקסימלית).

7.2 רשימת אירועי מערכת (System Events)

UrlAnalysisResultReceived: התקבל ניתוח URL משכבת המכשיר.

TrackUrlAlertReceived: אירוע מעקב (Click/Surf) מדומיין נעקב.

RemoteAccessAlertReceived: זיהוי שינוי במצב השליטה מרחוק.

ImmediateDangerDetected: טריגר קריטי המפעיל את פרוטוקול החסימה.

OtpInterceptionTriggered: זוהה ניסיון גניבת קוד אימות.

BlackScreenActivated: החלת מנגנון הסתרת מידע רגיש.

SetTrackedDomains: הפצת רשימת דומיינים למעקב/חסימה לכלל מכשירי המשתמש

UserIsTargetedAlertReceived: פרטי המשתמש נמצאו ברשימות שיווק

התרעת UserIsTargetedAlert מופעלת בפעם הראשונה שפרטי המשתמש נמצאו ברשימות שיווק. ואז user property בשם IsTargeted מקבל ערך true. במילים אחרות: UserIsTargetedAlert יופעל רק למשתמש עבורו IsTargeted=false.
