מערכת ההגנה מפני הונאות של ASPS

הגדרות בסיסיות

מערכת ההגנה מפני הונאות מגינה על משתמש על ידי ניטור התקשורת שלו במכשירים devices כמו מחשב וטלפון חכם.

למשתמש יש מכשירים שהמערכת משייכת אליו.

מערכת ההגנה מפני הונאות כוללת 3 שכבות:

1.    שכבת המכשיר Device Layer

2.    שכבת המשתמש User Layer

3.    שכבת מודיעין Intelligence Layer

מתחילים עם התראה ברמת מכשיר DeviceAlert.

שכבת המכשיר Device Layer

סוגי התראות מכשיר DeviceAlert:

UrlAlert – כאשר המשתמש גולש לכתובת urlאשר הדומיין domain שלו לא נמצא ברשימת domains לעקיבה TrackedDomains, (בדיקה וציון רמת סיכון)

TrackUrlAlert – כאשר המשתמש (הדפדפן)  גולש לכתובת url אשר הדומיין domain שלו נמצא ברשימת domains לעקיבה TrackedDomains, או  כאשר כבר נמצא בדף, המשתמש מקליק על קישור או שולח טופס

RemoteAccessAlert – שימוש בתוכנות שליטה מרחוק  - שימוש לרעה בשילוב עם גלישה באתרים רגישים או פעולות שונות במכשיר (כמו פתיחת ארנק קריפטו)

PhoneAlert – מספרי טלפון של שיחות (בדיקת fake number, blacklisted)

טיפול בהתראות

לכל משתמש User נוצר מופע instance של כל אחד מהאובייקטים הבאים:

UDUser

UDAnalysisManager

UDAnalysis

UDUserAnalyzer

UDAnalysisManager:

מקבל הודעות מסוג  DeviceAlertReceived ומנתב את ההתראה ל UDAnalysis,

UDAnalysis עושה:

מפעיל analyzer המתאים לפי סוג ה DeviceAlert (לדוגמא UDUrlAnalyzer).

מחשב רשימה של ProtectiveActions

שולח הודעה notification ל device עם תוצאה ורשימת ה ProtectiveActions (אם צריך).

שולח הודעה לכל ה device האחרים של המשתמש עם  ProtectiveActions מתאימים לכל אחד (אם צריך).

מרים DomainEvent מסוג AnalysisResultReceived.

UDUserAnalyzer:

שייך ל User Layer. נרשם ל DomainEvents מסוג AnalysisResultReceived ונכנס לפעולה בכל פעם שמתקבלת תוצאת אנליזה חדשה (של DeviceAlert)  – מבצע אנליזה ברמת המשתמש User Layer.

פעולות

טיפול בדיווחי זמן-אמת ממכשירים של המשתמש:	Handle DeviceAlerts

דיווח גלישה UrlAlert	HandleUrlAlertReceived

מבצעת בדיקת URL ומחשבת ציון  risk-assessment לפי פרמטרים שזיהתה (רישום domain, blacklisted, תוכן ועוד), לפי תוכן הדף מסווגת את האתר לקטגוריה, ומנסה לזהות סוג הונאה.

לאחר בדיקת ה URL – שולחת למכשיר ציון, ואם נדרש גם פעולות הגנה ProtectiveActions כמו התראה במכשיר (ויזואלית או קולית), חסימה, שליחת הודעה וכו'. ה ProtectiveActions נקבעים לפי הגדרות המערכת והעדפות שהמשתמש בעל החשבון מגדיר

דיווח הפעלת תוכנת שליטה מרחוק  RemoteAccessAlert 		HandleRemoteAccessAlertReceived

מעדכנת סטטוס שליטה מרחוק  RemoteAccess ב instance של ה UDUser של המשתמש הספציפי.

Dictionary<string, RemoteAccessAnalysisResultVm>	 RemoteAccessStatus

ה UDUser מחזיק סטטוס RemoteAccess לכל DeviceUid של ה User.

בדיקת מספרי שיחות טלפון:

שיחה נכנסת

בדיקת מספר ידוע לשימצה blacklisted number

שכבת המכשיר Device Layer

סוגי התראות מכשיר DeviceAlert:

a.     התראת UrlAlert – כאשר המשתמש גולש בכתובת url (בדיקה וציון רמת סיכון)

b.     התראת RemoteAccessAlert – שימוש בתוכנות שליטה מרחוק  - שימוש לרעה בשילוב עם גלישה באתרים רגישים או פעולות שונות במכשיר (כמו פתיחת ארנק קריפטו)

c.     התראת PhoneAlert – שיחות נכנסות (בדיקת fake number, VOIP, מדינה)

d.     ה backend מבצע אנליזה (ברמת ה device), שולח notification למכשיר עם ProtectiveActions (אם צריך) ומרים DomainEvent מסוג AnalysisResultReceived.

e.     ה UDUserAnalyzer מאזין ל DomainEvents מסוג AnalysisResultReceived ונכנס לפעולה – מבצע אנליזה ברמת המשתמש.

פעולות

טיפול בדיווחי זמן-אמת ממכשירים של המשתמש:	Handle DeviceAlerts

דיווח גלישה UrlAlert	HandleUrlAlertReceived

מבצעת בדיקת URL ומחשבת ציון  risk-assessment לפי פרמטרים שזיהתה (רישום domain, blacklisted, תוכן ועוד), לפי תוכן הדף מסווגת את האתר לקטגוריה, ומנסה לזהות סוג הונאה.

לאחר בדיקת ה URL – שולחת למכשיר ציון, ואם נדרש גם פעולות הגנה ProtectiveActions כמו התראה במכשיר (ויזואלית או קולית), חסימה, שליחת הודעה וכו'. ה ProtectiveActions נקבעים לפי הגדרות המערכת והעדפות שהמשתמש בעל החשבון מגדיר

דיווח הפעלת תוכנת שליטה מרחוק  RemoteAccessAlert 		HandleRemoteAccessAlertReceived

מעדכנת סטטוס שליטה מרחוק  RemoteAccess ב instance של ה UDUser של המשתמש הספציפי.

Dictionary<string, RemoteAccessAnalysisResultVm>	 RemoteAccessStatus

ה UDUser מחזיק סטטוס RemoteAccess לכל DeviceUid של ה User.

בדיקת מספרי שיחות טלפון:

שיחה נכנסת

בדיקת מספר ידוע לשימצה blacklisted number

השוואה לרשימת מספרי טלפון ידועים .

ליצור Entity חדש בדאטבייס ל BlacklistedPhoneNumber

פירוט class חדש ל BlacklistedPhoneNumber למטה.

בדיקת מספר מזוייף fake number

אם המספר מתגלה כמזוייף fake number – שולחת למכשיר הודעה notification עם ProtectiveActions כמו התראה במכשיר (ויזואלית או קולית), חסימה, שליחת הודעה וכו'. ה ProtectiveActions נקבעים לפי הגדרות המערכת והעדפות שהמשתמש בעל החשבון מגדיר.

שיחה יוצאת

ניטור קישורים זדוניים בהודעות שהמשתמש מקבל (מייל, סמס, ווטסאפ)

כניסה לחשבונות ב offline וחיפוש קישורים זדוניים.

הכניסה לחשבונות לבדיקת קישורים בהודעות נעשית בתדירות שמוגדרת במערכת (system-default) והעדפות שהמשתמש בעל החשבון מגדיר.

כל קישור נבדק ומקבל ציון ותוצאת אנליזה כמו בבדיקת UrlAlert.

כל תוצאות האנליזות ב Device Layer נשמרות בדטאבייס.

סעיף זה כבר ממומש היום. AnalysisPersistanceActor מאזין ל DomainEvent מסוג AnalysisResultReceived, ושומר בדטאבייס.

הוספת מוד עקיבה TrackMode לתוסף Extension

נוסף class חדש: TrackMode - ראה למטה

שכבת המשתמש User Layer

ברמת המשתמש User Layer, האנליזה בוחנת את התמונה הגדולה: דיווחים מכל מכשירי המשתמש לאורך זמן ופרופיל הסיכון הספציפי של המשתמש.

להכניס class חדש: UserRiskProfile שיכיל ציונים ומשקלים של מדדים שונים שמשפיעים על רמת הסיכון בו המשתמש נתון בסיטואציות שונות!!!

FraudUrlTracker

להוסיף שירות IBackgroundService חדש בשם FraudUrlTracker.

FraudUrlTracker ירשם ל DomainEvents מסוג RiskyUrlFound.

הוא יקבל url ואת הציון שקיבל באנליזה ו ה key שלה UrlAnalysisResultVm שיהיה בתוך ה RiskyUrlFound.

FraudUrlTracker ישמור את ה url בדטאבייס בטבלת RiskyUrls. ראה RiskyUrlEntity ברשימת ה classes החדשים למטה.

FraudUrlTracker ישמור את תוכן כל הדפים שיש באותו אתר או subdomain בטבלה בשם RiskyUrlPages.

RiskyUrl: Entity

{

Key		Key

string		Url

string		Domain

Key		SourceUserKey

DateTime	DateCreated

bool?		IsDeleted

}

RiskyUrPagel: Entity

{

Key		Key

string		Url

string		Domain

string		ContentText

string		ContentHtml

Key		RiskyUrlKey

DateTime	DateCreated

bool?		IsDeleted

string		Locale

string?		Proxy

}

כאשר ה FraudUrlTracker מקבל RiskyUrlFound  צריך שתהיה לו פונקציה HandleRiskyUrlFound. שעושה:

מכניס url, domain לטבלה RiskyUrls

FraudUrlTracker יפעיל analyzer חדש: RiskyUrlAnalyzer שמביא את תוכן כל הדפים שמוצא באתר וגם מחפש בכל הדומיין. כל url שעבורו הוא מביא תוכן הוא שומר בטבלה RiskyUrlPages. יהיה כתוב ב pythyon.

אחרי ששמר את כל הדפים החדשים - מרים DomainEvent מסוג חדש: RiskyUrlPagesAdded.

ASView

להוסיף property מסוג RiskyUrlPages שתהיה List של RiskyUrlPageView.

יאזין ל RiskyUrlPagesAdded  ותהיה לו פונקציה HandleRiskyUrlPagesAdded שתוסיף אותם ל RiskyUrlPages

UDUserAnalyzer

ניטור תוצאות אנאליזה של דיווחי מכשירים DeviceAlert של המשתמש:

Handle AnalysisResultReceived

טיפול ב תוצאת ניתוח דיווח גלישה UrlAlert	UrlAnalysisResultVm

RiskyUrlScoreThreshold  הוא פרמטר ב SystemConfiguration

אם ה Score חורג מ RiskyUrlScoreThreshold:

אם התוצאה אינה מ ה cache  יבדוק אם אותו domain כבר קיים ב ASView.RiskyUrlPages. אם לא קיים:

- ירים DomainEvent מסוג (חדש) RiskyUrlFound.

יהיה שירות IBackgroundService חדש בשם FraudUrlTracker שיאזין ל RiskyUrlFound

בכל מקרה: יפתח ScamInProgress עבור אותו user ואותו domain.

טיפול ב תוצאת ניתוח דיווח גלישה TrackUrlAlert	TrackUrlAnalysisResultVm

זה המקום לזהות תרחישי הונאה בתהליך.

למשל - שהמשתמש עבר מדף מודעה חשודה (עם ציון risk גבוה)  לדף עם טופס להשאיר פרטים או רישום או הצעה מפתה.

כאן אפשר לנתח את התכנים של הדפים. הם זמינים בטבלה RiskyUrlPages (ורשימה מקבילה שתפתח ב ASView).

טיפול ב תוצאת ניתוח דיווח גישה מרחוק RemoteAccessAlert	RemoteAccessAnalysisResultVm

עידכון סטטוס שליטה מרחוק  RemoteAccess ב instance של ה UDUser של המשתמש.

Dictionary<string, RemoteAccessAnalysisResultVm>	RemoteAccessStatus

ה UDUser מחזיק סטטוס  RemoteAccess עדכני לכל DeviceUid של ה User.

טיפול בתוצאת בדיקת מספרי שיחות טלפון:

שיחה נכנסת

אם מספר ידוע לשימצה blacklisted number

אם מספר מזוייף fake number

שיחה יוצאת

אם מספר ידוע לשימצה blacklisted number

זיהוי סכנה מיידית

זיהוי הונאה בתהליך

חישוב ציון סיכון כולל למשתמש

חישוב פעולות הגנה ProtectiveACtion שצריך לשלוח למכשירי המשתמש

הפצת notifications הודעות למכשירי המשתמש

משלוח הודעות למשתמש (מייל, סמס, ווטסאפ וכו', לפי העדפות שהמשתמש מגדיר בחשבון שלו).

פעולות הגנה ProtectiveActions יכול להיות משהו כמו התראה במכשיר (ויזואלית או קולית), חסימה, שליחת הודעה וכו'. ה ProtectiveActions נקבעים לפי הגדרות המערכת והעדפות שהמשתמש בעל החשבון מגדיר

שכבת המשתמש User Layer מחזיקה מופע instance של המשתמש (UDUser), בו היא צוברת תוצאות הניתוחים של דיווחי המכשירים ומתחזקת סטטוס סיכון עדכני של המשתמש.

שכבת המשתמש User Layer מאזינה לתוצאות בדיקות שמתקבלות משכבת המכשיר.

עם קבלת תוצאת אנליזה של DeviceAlert  דרך DomainEvent  מסוג AnalysisResultReceived היא מעדכנת את הדאטה ב UDUser ומחשבת מחדש סיכונים וציון risk סופי עבור המשתמש User.

אנליזה ברמת המשתמש בוחנת את התנהגות המשתמש בכל המכשירים לאורך זמן (היסטוריה של גלישות, שיחות ממספרי טלפון, שימוש ב תוכנות גישה מרחוק והודעות שאותו אדם קיבל עם קישורים מסוכנים במייל, sms, WhatsApp וכו').

מטרות האנליזה:

1.     לזהות תרחיש של סכנה מיידית.

2.     לזהות הונאה בתהליך ואת סוג ההונאה.

במידה וזיהה מצב מסוכן או הונאה בתהליך – לשגר התראות ופעולות מנע ProtectiveActions לפי הגדרות המערכת והגדרות ספציפיות של המשתמש.

+

זיהוי תרחיש של סכנה מיידית:

+

כרגע מוגדר תרחיש סכנה מיידית אחד. 2 תנאים conditions נדרשים:

1.     מכשיר נשלט מרחוק (סטטוס remote-access מראה session פתוח בכיוון פנימה)

2.     באותו מכשיר יש דפדפן פתוח באתר רגיש (כמו חשבון בנק או מערכת מסחר) והמשתמש מחובר

זיהוי תרחישי הונאות בתהליך

הונאת TechSupportScam והונאת InvestmentScam

הדפדפן טוען דף ושולח UrlAlert. האנליזה ב backend מסוג UrlAnalysisResult מסווג את הדף כ:

אזעקת שווא על תקלה מדומה במכשיר המשתמש (בתוצאות האנליזה UrlAnalysisResultVm  מופיע TechSupportBait).

או /

פיתיון להשקעה עם רווח לא מציאותי. (ב UrlAnalysisResultVm  מופיע InvetmentBait)

ה UDUserAnalyzer מוסיף לרשימת ScamsInProgress  ב UDUser פריט ScamInProgress חדש (אם לא קיים פריט עם אותו Url)

ובכל מקרה יוצר ScamProgressItem חדש ומוסיף אותו ל ScamProgressItems ב ScamInProgress (הקיים או זה שיצר).

אם יוצר ScamInProgress חדש – נותן לו Key  ייחודי עם GUID.

הפצת DomainEvent חדש:  ScamInProgressAdded

ה backend שולח הודעה לכל התוספים בכל מכשירי המשתמש עם רשימת TrackedDomains (אולי ScamProgressItems). למעשה - שולח לכל ה agents. ה agent דואג להעביר לכל התוספים באותו מכשיר.

ה backend שולח הודעה לתוסף עם command TrackMode=Click עם ה Key של ה ScamInProgress עבור אותו browser tab.

התוסף ישלח דיווח (מסוג TrackUrlAlert) על כל click וכל ניווט של המשתמש בכל domain שמופיע בכל url ברשימת ScamProgressItems של ה ScamInProgress הספציפי לפי ה GUID.

UDUserAnalyzer  יוסיף לרשימת TrackedDomains ב UDUser את ה domain של ה url ב ScamProgressItem שיצר (אם לא קיים ברשימה). (זה הדומיין המסוכן).

ה UDUserAnalyzer ישלח לכל ה devices הודעה notification מסוג SetTrackedDomains

ה domain המסוכן ישמר בטבלה בדאטבייס שנקראת RiskyDomains.

שדות בטבלה:

RiskyDomainEntity : Entity

{

Key 		Key

string		Domain

DateTime	DateCreated

DateTime	DateDeleted

}

כאשר ה backend הוסיף דומיין מסוכן לרשימה, הוא יאסוף תכנים של דפים (כתובות url) בדומיין (או באתר) הזה. אפשר להשתמש בתוכנה בשם DirBuster, או במודל AI שסורק קישורים באתר ומודל AI שני שמסכם תוכן ועוד אחד שמבין כוונה ומסווג.

זה יתבצע באופן הבא:

ה ExtendedUrlAnalyzer (או UDAnalyzer) ירים DomainEvent מסוג RiskyDomainFound.

יהיה BackgroundService שיאזין ל DomainEvent מסוג RiskyDomainFound.

אם לא קיים בטבלת RiskyDomains - יוסיף ואז יסרוק את כל הדפים באתר (דומיין) וישמור בטבלת RiskyDomainPages.

התכנים (טקסט) של כל הדפים בדומיין המסוכן ש ה backend יסרוק ישמרו בטבלה שנקראת RiskyDomainPages, ויהיה מצביע (pointer) לטבלת RiskyDomains.

RiskyDomainPage: Entity

{

Key		Key

string		RiskyDomain

string		Url

String		PageText

String		DateCreated

String		DateScanned

DateTime	DateDeleted

}

SetTrackedDomains

{

List<TrackedDomain> TrackedDomains

}

לאחר שגילה שהמשתמש גלש לדומיין מסוכן:

יוסיף אייטם חדש לרשימת TrackedDomains ב UDUser

ה backend ישלח notification מסוג פקודה SetTrackedDomains עם רשימת ה TrackedDomains של ה UDUser לכל המכשירים devices של אותו משתמש.

זיהוי ניסיונות Phishing

ניטור שוטף של הודעות שהמשתמש מקבל (מייל, סמס, ווטסאפ)

הודעות Email

ה Backend  UDUserAnalyzer יבדוק את תיבת הדואר הנכנס של חשבונות email של המשתמש שהגדיר בהעדפות (UserConfiguration) בכל פרק זמן שמוגדר בקונפיגורציה של המשתמש (שם הפרמטר EmailScanIntervalMin).

הודעות sms

ה agent במכשיר טלפון יבדוק הודעות שהמשתמש מקבל ב sms. זה יקרה עם כל הודעת sms שמתקבלת במכשיר.

הודעות WhatsApp

ה agent במכשיר טלפון יבדוק הודעות שהמשתמש מקבל ב WhatsApp. זה יקרה עם כל הודעת WhatsApp שמתקבלת במכשיר.

מה בודקים בהודעות:

1.     בדיקה של ה url ו ה domain (כמו בדיקה של UrlAlert).

2.     אם ה risk_score מצביע על סיכון (הציון חורג מערך סף שמוגדר בקונפיגורצית מערכת System Configuration) – ה backend ישלח התראות ופקודות ProtectiveActions ב notifications למכשירי המשתמש, לפי מה שמוגדר בקונפיגורציה (העדפות) של המשתמש.

הבטחת מסירה של הודעות - NotificationPersistance

ה backend ינהל מעקב אחרי הודעות ופקודות notifications ששלח לכל device ואם ה device אישר קבלה. הוא ישמור את זה בטבלה בזיכרון, ומדי פעם יעדכן טבלה בדאטבייס (למקרה של נפילה). אם ה backend  נפל - אז בעלה הוא יביא ל ASView את ההודעות שלא התקבלו.

אם ה device לא מחובר אז כאשר הוא יתחבר בפעם הבאה - ה backend ישלח ל device את כל ההודעות שעבורן לא קיבל אישור קבלה ack. פרמטרים: MaxForDevice, OutdateAge ילקחו מ ה appsettings או SystemConfiguration.

UDAnalysis של המשתמש יעקוב אחרי כל רשומות ה ScamInProgress, וסטטוס ה TrackMode. אם יקבל מ device UrlAlert שעבורו צריך היה לקבל גם ExtendedUrlReport, ולא התקבל – הוא ישלח שוב פקודת TrackMode=Click.

זיהוי Cloaking

שיטה:

להוריד תוכן מ url דרך proxies שונים בגיאוגרפיות שונות ולהשוות את התוכן והכוונה.

הגלישה חייבת להיות מזוהה כדפדפן

צריך להיות חלק מ ה urlAnalyzer

שינויים נדרשים בתוסף

התוסף יכיל משתנה חדש בשם TrackedDomains. זה List של משתנים מסוג TrackedDomain ראה הגדרה בהמשך.

את רשימת ה TrackedDomains  התוסף יקבל מ ה backend בהודעה notification.

ה backend ישלח notification מסוג SetTrackedDomains שתכלול List של TrackedDomain.

כאשר התוסף מקבל notification חדש עם SetTrackedDomains, הרשימה החדשה מחליפה את הישנה. אם מתקבל SetTrackedDomains עם null או רשימה ריקה [], ה TrackedDomains יהיה רשימה ריקה.

מה התוסף עושה עם הרשימה של TrackedDomains:

עבור כתובות url שהדומיין שלהם לא כלול ב TrackedDomains:

ברירת המחדל - התוסף ידווח UrlAlert ב TrackMode.Surf' ובמשך זמן מוגדר לא ישלח UrlAlert נוסף בגין אותו דומיין (שים לב - לא Url).

עבור כתובות url שהדומיין שלהם כלול ב TrackedDomains:

התוסף ידווח UrlAlert  או ExtendedUrlAlert  לפי השם שמצויין בשדה ReportType

(RemoteAccessAlert, UrlAlert, ExtendedUrlAlert)

מצב TrackMode.Surf:

ברירת מחדל.

התוסף שולח UrlAlert בעת גלישה פעם אחת ל Url מסויים, ובמשך זמן מוגדר לא ישלח UrlAlert נוסף בגין אותו דומיין (שים לב - לא Url).

מצב TrackMode.Click:

התוסף שולח UrlAlert או ExtendedUrlAlert (מוגדר בהמשך) בכל פעם שהמשתמש מקליק (click) על קישור בדף.

סוג ה alert נקבע לפי רשימת TrackedDomains. אם ה domain נמצא ברשימה - מה שכתוב בשדה ReportType. אם ה domain לא ברשימה - אז UrlAlert.

הוספת נתונים ב UrlAlert:

כאשר התוסף יוצר UrlAlert, להוסיף שדות:

DateTime	Timestamp

string		TabId

Int		Timezone

קונפיגורציה של התוסף:

משך הזמן בו התוסף לא ישלח UrlAlert אחרי ששלח ל url מסויים יוגדר ב config של התוסף:

int	UrlAlerSilenceIntervalMinutes

int	HighRiskThreshold

int	LowRiskThreshold

int	Version

כאשר התוסף מתחיל הוא שולח  בקשת קונפיגורציה (דרך ה agent) ל backend.

עידכון קונפיגורציה של התוספים מ ה backend:

ה backend שולח הודעה ל agent בשם SetExtensionConfiguration.שכוללת קנפיגורציה של תוסף. ה agent מעביר לכל התוספים באותו מכשיר את הקופיגורציה.

ב admin יהיה דף שמאפשר לעדכן קונפיגורציה של תוסף ולשלוח אותה לכל התוספים (דרך כל ה agents).

התוסף של כל דפדפן מעדכן את קובץ הקונפיגורציה המקומי שלו ומאתחל את עצמו.

אפשרות אחרת - יהיה קובץ קונפיגורציה אחד ב agent. כל התוספים יקחו את הקונפיגורציה מ ה agent.

נתונים שהתוסף מקבל מ ה Agent:

בהודעות ה keep-alive בין התוסף ל agent ה agent יעביר לתוסף Timezone (בנוסף ל DeviceUid שהוא כבר מקבל)

רשימת BrowserTabs:

לכל רשומת BrowserTab להוסיף את המזהה של ה tab - השדה בשם TabId.

סוג חדש של התראה: TrackUrlAlert

TrackUrAlert

{

DateTime      	Timestamp

string		DeviceUid

string              	Url

string?           	FromUrl	(The url from which it 	ה url ממנו הגיע, זה שבו עשו קליק)

TimeSpan?	Duration	(כמה זמן המשתמש היה בדף)

string 	           	ScamInProgressKey	(Taken from TrackedDomains)

string 		IPAddress	IP address of the device

string 		UserAgent

string?		TabId

Int?		Timezone

}

שינויים נדרשים ב Agent:

בהודעות ה keep-alive בין התוסף ל agent, ה agent יעביר לתוסף גם Timezone (בנוסף ל DeviceUid).

להוסיף Timestamp כאשר שולח הודעת RemoteAccessAlert.

ניהול קונפיגורציה של תוספים

עידכון קונפיגורציה של התוספים מ ה backend:

ה backend שולח הודעה ל agent בשם SetExtensionConfiguration.שכוללת קנפיגורציה של תוסף. ה agent מעביר לכל התוספים באותו מכשיר את הקופיגורציה.

ב admin יהיה דף שמאפשר לעדכן קונפיגורציה של תוסף ולשלוח אותה לכל ה agents.

ה agent מעדכן קובץ קונפיגורציה של תוספים שנמצא אצלו.

לכל קונפיגורציה שמתקבלת מ ה backend יש שדה int בשם Version. ה agent לא יעדכן קונפיגורציה ל version נמוך יותר מהקיים אצלו.

התוסף של כל דפדפן מבקש את קובץ הקונפיגורציה מ ה agent.

אם תוסף פועל ללא agent הוא ישתמש בקונפיגורציה ברירת מחדל שתהיה לו.

שינויים נדרשים UrlAnalyzer

זיהוי Cloaking

ה UrlAnalyzer יכיל פרמטר חדש בשם CheckCloaking . אם true:

ה UrlAnalyzer יבצע בדיקת Cloaking לכל  ויוסיף אותו לאובייקט התוצאה UrlAnalysisResultVm הקיים.

שינויים נדרשים ב Backend

RealTimeAlertListener:

בכל הודעה מסוג DeviceAlert שמקבל מ device - לעדכן שדה ReceivedAt. זה ישים ל DeviceAlerts מסוג RemoteAccessAlert, UrlAlert, ExtendedUrlAlert.

טיפול ב DeviceAlert מסוג חדש:   ExtendedUrlAlert

כמו הטיפול ב UrlAlert - להרים DomainEvent מסוג DeviceAlertReceived

AlertPersistanceActor:

כאשר מתקבל DomainEvent מסוג DeviceAlertReceived ו ה DeviceAlert הוא מסוג ExtendedUrlAlert   אז לשמור את ה ExtendedUrlAlert בדאטבייס.

UDAnalysisManager:

מקבל הודעות מסוג  DeviceAlertReceived ומנתב את ההתראה ל UDAnalysis,

UDAnalysis:

להוסף HandleExtendedUrlAlertReceived ב UDUrlAnalyzer.

ליצור ExtendedUrlAlertAnalyzer (בקובץ .cs), מקביל ל UDUrlAnalyzer.

UDUserAnalyzer:

ניטור תוצאות אנאליזה של דיווחי מכשירים DeviceAlert של המשתמש:

Handle AnalysisResultReceived

טיפול ב תוצאת ניתוח דיווח גלישה UrlAlert	UrlAnalysisResultVm

אם ה Score נמוך מ RiskyUrlScoreThreshold והתוצאה אינה מ ה cache   - ירים DomainEvent מסוג (חדש) RiskyUrlFound.

RiskyUrlScoreThreshold  הוא פרמטר ב SystemConfiguration

יהיה שירות IBackgroundService חדש בשם FraudUrlTracker שיאזין ל RiskyUrlFound

להוסיף שירות IBackgroundService חדש בשם FraudUrlTracker.

FraudUrlTracker ירשם ל DomainEvents מסוג RiskyUrlFound.

הוא יקבל url ואת הציון שקיבל באנליזה ברמת מכשיר UrlAnalysisResultVm שיהיה בתוך ה RiskyUrlFound.

FraudUrlTracker ישמור את ה url בדטאבייס בטבלת RiskyUrls. ראה RiskyUrlEntity ברשימת ה classes החדשים למטה.

FraudUrlTracker ישמור את תוכן כל הדפים שיש באותו אתר או subdomain בטבלה בשם RiskyUrlPages.

טיפול ב תוצאת ניתוח דיווח גישה מרחוק RemoteAccessAlert	RemoteAccessAnalysisResultVm

עידכון סטטוס שליטה מרחוק  RemoteAccess ב instance של ה UDUser של המשתמש.

Dictionary<string, RemoteAccessAnalysisResultVm>	RemoteAccessStatus

ה UDUser מחזיק סטטוס  RemoteAccess עדכני לכל DeviceUid של ה User.

טיפול בתוצאת בדיקת מספרי שיחות טלפון:

בדיקות:

שיחה נכנסת

אם מספר ידוע לשימצה blacklisted number

אם מספר מזוייף fake number

שיחה יוצאת

אם מספר ידוע לשמצה blacklisted number

אם blacklisted או fake:

ה UDUserAnalyzer עושה:

בודק אם קיים ברשימת ScamsInProgress  ב UDUser פריט שמשוייך לאותו מספר טלפון.

יוצר ScamInProgress חדש – (Key  ייחודי עם GUID ואת מספר הטלפון כ entry-point) ומוסיף אותו לרשימת ScamsInProgress  ב UDUser.

בכל מקרה יוצר ScamProgressItem חדש ומוסיף אותו ל ScamProgressItems ב ScamInProgress (הקיים או זה שיצר).

מפיץ DomainEvent חדש:  ScamInProgressAdded

זיהוי סכנה מיידית Immediate Danger

כרגע מוגדר תרחיש סכנה מיידית יחיד:

אם באחד המכשירים של המשתמש ה RemoteAccessStatus מצביע על Session פתוח בכיוון פנימה (המכשיר נשלט מרחוק):

בודק את כל ה BrowserTabs ש ה device שלח ב RemoteAccessAlert.

אם ה url של אחד או יותר מ ה tabs מזוהה כ SensitiveUrl (כמו החשבון שלו בבנק או אתר השקעות או מסחר)  או (במקרה של טלפון) ה device מדווח על אפליקציה של הבנק או השקעות שרצה.

זו אינדיקציה למצב סכנה מיידית.

תישלח הודעה notification למכשיר הספציפי עם ProtectiveAction כפי שמוגדר בהעדפות המשתמש (User Configuration) לתרחיש לכל מכשירי המשתמש.

כרגע מוגדר תרחיש סכנה מיידית אחד. 2 תנאים conditions נדרשים:

1.     מכשיר נשלט מרחוק (סטטוס remote-access מראה session פתוח בכיוון פנימה)

2.     באותו מכשיר יש דפדפן פתוח והמשתמש מחובר באתר רגיש (כמו חשבון בנק או מערכת מסחר)

זיהוי הונאה בתהליך

חישוב ציון סיכון סיכון Risk Score כולל למשתמש

חישוב פעולות הגנה ProtectiveActions שצריך לשלוח למכשירי המשתמש

הודעות notifications למכשירי המשתמש

הודעות notifications למשתמש

(מייל, סמס, ווטסאפ וכו', לפי העדפות שהמשתמש מגדיר בחשבון שלו).

פעולות הגנה ProtectiveActions יכול להיות משהו כמו התראה במכשיר (ויזואלית או קולית), חסימה, שליחת הודעה וכו'. ה ProtectiveActions נקבעים לפי הגדרות המערכת והעדפות שהמשתמש בעל החשבון מגדיר

AnalysisPersistanceActor:

כאשר מתקבל DomainEvent מסוג AnalysisResultReceived ו ה AnalysisResult הוא מסוג ExtendedUrlAnalysisResult אז  לשמור את ה ExtendedUrlAnalysisResult  בדאטבייס.

ב AnalysisPersistanceActor.HandleDeviceAlertReceived:

להוסיף   case ExtendedUrlAlert

ASView

ב ASView.HandleDeviceAlertReceived:

להוסיף   case ExtendedUrlAlert

UDUser:

להוסיף properties:

List<ScamInProgress> ScamsInProgress

List<TrackedDomain> TrackedDomains

bool IsScammed	(שדה זה מציין אם המשתמש נפל קורבן להונאה בעבר. המשתמש יציין את הנתון בעת ההרשמה לשירות)

User:

להוסיף properties:

bool IsScammed:	(שדה זה מציין אם המשתמש נפל קורבן להונאה בעבר. המשתמש יציין את הנתון בעת

ההרשמה לשירות ויוכל לעדכן בהמשך)

bool IsTargeted:	(ה backend יעדכן שדה זה בהתאם לממצאים בזמן אמת)

DeviceAlert:

להוסיף שדות

int?		Timezone

DeviceAlertEntity:

להוסיף שדות:

int		Timezone

DateTime	ReceivedAt

שדה ReceivedAt ימולא על ידי ה RealTimeAlertListener. מציין את הזמן שההודעה התקבלה.

UrlAlert:

להוסיף שדות

string 		TabId		זה מזהה ה tab של הדפדפן

התוסף שולח UrlAlert. ה backend שומר בדאטבייס UrlAlertEntity.

התוסף ישלח UrlAlert עם שדה Timestamp.

UrlAlertEntity:

להוסיף שדות:

string 		TabId		זה מזהה ה tab של הדפדפן

יצירת classes חדשים ב backend:

ScamInProgress

{

String	 			Key,

IEnumerable<ScamProgressItem> 	ScamProgressItems,

int					Confidence,

ScamType				ScamType,

}

ScamProgressItem

{

String	 			Key,

string 				PrevKey,

string 				DeviceUid,

DateTime			Timestamp,

string 				Url,

RiskyContentCategory	ContentCategory,

ScamProgressItemType	Type,

}

enum TrackMode

{

None= 0,

Surf= 1,

Click=2,

}

enum 	RiskyContentCategory

{

Unknown 	  	 = 0,

TechSupportScamAd = 1,

InvetmentScamAd	 = 2,

RecoveryScamAd	 = 3,

TechSupportScamForm = 4,

InvetmentScamForm	 = 5,

RecoveryScamForm	 = 6,

PhishingForm		= 7,

}

enum ScamProgressItemType

{

Unknown = 0,

Ad = 1,

PersonalDetailsForm	 = 2,

PersonalDetailsFormSubmit	 = 3,

IncomingCallFakeNumber = 4,

}

TrackedDomain

{

string 			Domain,

string 			ScamInProgressKey,

TrackMode	 	TrackMode,

DeviceReportType	ReportType

}

enum DeviceReportType

{

Unknown	=0,

RemoteAccessAlert = 1,

UrlAlert = 2,

TrackUrlAlert = 3,

}

סוג חדש של התראה: TrackUrAlert

זה מה ש התוסף שולח:

TrackUrAlert

{

DateTime      	Timestamp

string		DeviceUid

string              	Url

string?           	FromUrl	(The url from which it 	ה url ממנו הגיע, זה שבו עשו קליק)

TimeSpan?	Duration	(כמה זמן המשתמש היה בדף)

string 	           	ScamInProgressKey	(Taken from TrackedDomains)

string 		IPAddress	IP address of the device

string 		UserAgent

string?		TabId

Int?		Timezone

}

זה ה Entity שישמר ב database:

ExtendedUrlAlertEntity

TrackUrAlertEntity : DeviceAlertEntity

{

string              	Url

string?           	FromUrl	(The url from which it 	ה url ממנו הגיע, זה שבו עשו קליק)

TimeSpan?	Duration	(כמה זמן המשתמש היה בדף)

string 	           	ScamInProgressKey	(Taken from TrackedDomains)

string 		UserAgent

string?		TabId

}

וכמובן כל שאר ה properties ששייכים ל DeviceAlertEntity.

BlacklistedPhoneNumber

{

DateTime      	Timestamp

int		CountryCode

int              	AreaCode

int              	Number

DateTime	DateCreated

string		Source

}

BlacklistedPhoneNumberEntity : Entity

{

DateTime      	Timestamp

int		CountryCode

int              	AreaCode

int              	Number

DateTime	DateCreated

DateTime	DateDeleted

string		Source

bool		IsDeleted

}

שכבת מודיעין Intelligence Layer

רשימות לידים ב darknet - קבלת מידע שפרטי המשתמש נמצאים ברשימות שנמכרות ב darknet.

רשימות אתרים ודומיינים זדוניים - שיוך קישורים שמשתמש מקבל בהודעות (מייל, סמס, ווטסאפ ועוד) למידע על דומיינים זדוניים

מספרי טלפון מזוייפים - שיוך מספרי ווטספ מהם המשתמש קיבל הודעות, למודיעין שלנו על מספרים שכנראה ישמשו להונאות.

ניהול מדיניות Cache

Admin – Tools

Clear Cache

Initialize View

Admin – Device Alert Simulator

מטרה:

כלי למפעיל לדמות תהליכים לאורך זמן. מאפשר הגדרה והפעלה של סדרת הודעות ממכשירים UserDevices של משתמש User.

שיטה:

יצירת אובייקט Simulation

Simulation: Entity

{

Key		Key

DateTime	DateCreated

string		Name

Key		CreatorKey

string		Description

SimulationStep[]	Steps

}

אובייקט Simulation כולל סדרה של steps: SimulationStep

SimulationStep:

{

int		Sequence

TimePeriod	Delay

string		DeviceUid

string		UserId	(The User Key value)

string		AlertType

DeviceAlert	Alert

}

כדי ליצור סימולציה באדמין מסך Admin-Simulations

מסך Simulations

מציג רשימת סימולציות Simulations

שדות : name, date modified, created by, description, key

הרשימה מאפשרת filter חיפוש לפי טקסט בשדות name, deiscription, createdBy ו key

הרשימה מאפשרת sort לפי name, date modified, createdBy

בכל שורה ברשימת הסימולציות יש כפתורים לעריכה, מחיקה והפעלה של הסימולציה.

כפתור עריכה פותח מסך עריכת סימולציה שכולל שדות:

מסך Create/Edit Simulation:

שדות Name, description

תחת Steps יש רשימה של SImulationSteps ריקה ויש כפתור "+" ליצירת Step חדש.

מסך Edit Simulation Step יכול להיפתח בתוך רשימת ה steps או ב dialog חדש.

במסך Edit Simulation Step המשתמש מגדיר:

בחירת user	(תיבת טקסט autocomplete מחפש בשדות שם פרטי, שם משפחה, טלפון, מזהה UserId) וגם (במעבר ל steps הבאים השדה כבר יכיל את הבחירה האחרונה)

בחירת device 	( בחירה של מכשיר של ה user שנבחר dropdown)

בחירת סוג הודעה (UrlAlert, RemoteAccessAlert, TrackUrlAlert)

השדות בטופס משתנים לפי סוג ה DeviceAlert שנבחר

שדה נוסף - השהיית זמן אחרי step קודם יוצג עם כל סוגי ההודעות.

כל step מקבל ערך ל sequence לפי הסדר שנוצר.

המשתמש מאשר ואז נוסף SimulationStep לרשימת ה steps ב Simulation.

במסך עריכת Simulation המשתמש יכול למחוק step מרשימת ה SimulationSteps.

וגם לשנות את סדר ה steps עם drag-and-drop. שדה "Sequence" של כל step משתנה לפי המיקום ברשימה.

המשתמש לוחץ Save והסימולציה Simulation נשמרת בדאטבייס. רשימת ה steps נשמרת בשדה text בשם "Simulation Steps" בפורמט json.

Background Services

Archive Service

העתקה של רשומות שאיבדו תוקף לטבלת ארכיון מתאימה לפי סוג הרשומה.

לדוגמה: רשומת UrlAnalysisResult ש ה Timestamp שלה ישן יותר מ UrlAnalysisResultExpirationDays  תתווסף לטבלת AnalysisResultsArchive ותימחק מטבלה AnalysisResults, ובאותו אופן, רשומות מטבלה DeviceAlerts הקשורות יעברו לטבלה DeviceAlertArchive.

int	UrlAnalysisResultExpirationDays

System Configurations

הגדרות מערכת כוללות:

הגדרות כלליות

int	UrlAnalysisResultExpirationDays

int	RiskyDomainPageScrapingExpirationDays

int 	HighRiskThreshold

int 	MediumRiskThreshold

משך הזמן בו התוסף לא ישלח UrlAlert אחרי ששלח ל url מסויים יוגדר ב config של התוסף:

int 	UrlAlerSilenceIntervalMinutes

ברירות מחדל להגדרות משתמש:

מה לעשות מתי. התנהגות המערכת באירועי סיכון שונים.

הגדרה כללית: הגדרת ברירות מחדל לפעולות הגנה ProtectiveActions ב 3 תחומי הערכת הסיכון  risk_assessment  למשתמש : none-medium-high

או

הגדרה פרטנית: הגדרת ברירות מחדל לפעולות הגנה ProtectiveActions בתרחישים שונים (צריך להגדיר).

חריגים : דומיינים ומספרי טלפון מהם האנליזה של המערכת תתעלם (לכל המשתמשים).

User Configurations

הגדרות משתמש כוללות:

מה לעשות מתי. התנהגות המערכת באירועי סיכון שונים.

הגדרה כללית: הגדרת פעולות הגנה ProtectiveActions ב 3 תחומי הערכת הסיכון  risk_assessment  למשתמש : none-medium-high

או

הגדרה פרטנית: הגדרת פעולות הגנה ProtectiveActions בתרחישים שונים (צריך להגדיר).

חריגים : דומיינים ומספרי טלפון מהם האנליזה של המערכת תתעלם עבור אותו משתמש.

גיאוגרפיה ושפות: הגדרת locale ו timezone (שפה ואזור זמן)

Version Control

פיצ'ר חדש: ניהול גירסאות המערכת השלמה כוללת את ה solution

שדה חדש - Version	0.0.0.0

לכל רכיב תוכנה במערכת ASPS יהיה מספר גירסה version (תחקור על זה).

C# VisualStudio Solution: Backend, WebApi, Admin

בפרוייקטים של ה solution ב C# תוסיף שדה Version בקובץ הפרוייקט "cproj.".

גם לתוכנות  ב python צריך להיות מס' גירסה שמקודם באופן אוטומטי. ספציפית:

Agent

Extension

כל האנלייזרים:

basic-url-analyzer.py

מסמכי md

תצוגת Version ב UI

באדמין Admin		ב dashboard יוצגו הנתונים הבאים:

ASPSBackend Version Number	 לדוגמה ver:0.1.0.2

WebApi  Version Number	 לדוגמה ver:0.1.0.2

Admin Version Number	 לדוגמה ver:0.1.0.2

ב Agent		בחלון UI שנפתח יוצג מס גירסה. לדוגמה ver:0.1.0.2

ב Extension		בחלון UI שנפתח יוצג מס גירסה לדוגמה ver:0.1.0.2

כאשר ה version מקודם - ליצור אוטומטית  קובץ readme.md לאותו version שיכיל את התיאורים של ה commits ב gitHub שנוספו מהגירסה הקודמת.

ה version יקודם כאשר עושים deploy ל production.

זה הרעיון הכללי. אני רוצה שתחשוב איתי איך

Auto-Update User Devices to Latest Version

Agent Desktop-Win:

ניהול גירסאות ב Agent Agent Desktop-Win:

עידכון אוטומטי של תוכנת ה Agent:

צד שרת Backend

להוסיף שדה מסוג string בשם Version ל entity UserDevice

להוסיף שדה Version ל DeviceInfo, וכך הוא יועבר בכל הודעה ש ה agent שולח.

עם כל הודעה ש ה RealtimeAlertlstener מקבל מ UserDevice הוא משווה את ה version בהודעה למשתנה בשם LatestVersion_Agent. אם ערך ה Version ב DeviceInfo בהודעה נמוך יותר(או שונה) מערך LatestVersion_Agent_Win אז ה Backend לא ימשיך לטפל ב DeviceAlert אלא יחזיר ל UserDevice notification מסוג חדש בשם VersionUpdateRequired. להלן המבנה

VersionUpdateRequired

{

string   	VersionRequestId

string   	OldVersion

string   	NewVersion

string   	DeviceUid

string   	Message

string   	DownloadPath

DateTime             	Timestamp

}

VersionUpdateRequest

{

string   	VersionRequestId

string   	OldVersion

string   	NewVersion

string   	DeviceUid

string   	Message

DateTime             	Timestamp

}

ה Backend ישמור את פרטי ה VersionUpdateRequired

כאשר יקבל בקשת הורדה download, הוא ישווה את פרטי ה VersionUpdateRequest לפי ה VersionRequestId.

אם תואם – יאפשר הורדה ויעדכן  את רשומת VersionUpdateRequest המתאימה

וגם יעדכן את שדה Version בטבלת UserDevices (לפי ה DeviceUid) ירים DomainEvent מסוג UserDeviceChanged. ה ASView יטפל ב domain event מסוג UserDeviceChanged ויעדכן את ה UserDeviceView המתאים.

אחרי העליה מחדש הוא ישלח מחדש את ההודעה האחרונה ששלח ובגינה קיבל את

צד המכשיר תוכנת ה Agent

להוסיף שדה Version ל DeviceInfo בכל הודעה ש ה agent שולח.

כאשר ה agent שולח DeviceAlert ומקבל תשובה notification מסוג VersionUpdateRequired:

הAgent  שומר את ההודעה האחרונה ששלח וכל ההודעות נוספות שיווצרו ברשימה ונשמרת בקובץ טקסט אותו ה agent יקרא אחרי עליה מחדש.

מוריד את גירסת התוכנה החדשה מ ה DownloadPath שבהודעת ה VersionUpdateRequired.

(ה DownloadUrl הוא ה DownloadPath בתוספת לכתובת ה backend איתו ה agent מתקשר.)

ה agent שולח VersionUpdateReqest  עם ה VersionRequestId או שזה ב .url

לאחר סיום ההורדה, ה agent מפעיל את עצמו מחדש אבל מהתוכנה בגרסה החדשה.

ScamInProgress

}

Key   	           	Key

ScamType    	ScamType

DateTime      	CreatedAt

String?           	DeviceUid

Key?                	TriggerAnalysisKey

ScamProgressItem[]  ScamProgressItems

}

ScamProgressItem

{

DateTime      	Timestamp

String              	Url

String              	FromUrl

int      	           	Sequence

string              	DeviceUid

{

enum ScamType

{

Unknown = 0,

Investment = 1,

TechSupport = 2,

Romance = 3,

}

enum ScamTools

{

Unknown = 0,

Phishing = 1,

FakePhoneNumber = 2,

VoiceCloning = 3,

Impersonation= 4,

}

ExtendedUrlReport

{

DateTime      	Timestamp

String              	Url

String?           	FromUrl

Timeframe?	Duration

String              	DeviceUid

Key? 	           	ScamInProgressKey

}

RiskyDomainPage: Entity

{

Key		Key

string		RiskyDomain

string		Url

String		PageText

String		DateCreated

String		DateScanned

DateTime	DateDeleted

}

SetTrackedDomains

{

List<TrackedDomain> TrackedDomains

}

RiskyDomainEntity : Entity

{

Key 		Key

string		Domain

DateTime	DateCreated

DateTime	DateDeleted

}

RiskyUrlEntity

{

string		Url

string 		Domain

DateTime	DateCreated

string		Source

bool?		IsDeleted

DateTime	DateDeleted

}

RiskyUrlPage

{

string			Url

string 			Domain

DateTime		DateCreated

string			Source

Bool?			IsDeleted

DateTime		DateDeleted

string			Locale

string			Text

string			Html

WebsiteCategoty	WebsiteCategoty

IntentCategoty		IntentCategoty

RiskScore

}

ScamInProgressAdded

{

Key		SpamInProgressKey

DateTime	Timestamp

Key		UserKey

}

ExtensionConfiguration

{

int	UrlAlerSilenceIntervalMinutes

int	HighRiskThreshold

int	LowRiskThreshold

int	Version

}

SystemConfiguration

{

Key		Key

String		Configuration	(json)

DateTime	DateCreated

bool?		IsDeleted

Int		Version

}

RiskyUrl: Entity

{

Key		Key

string		Url

string		Domain

Key		SourceUserKey

DateTime	DateCreated

bool?		IsDeleted

}

RiskyUrPagel: Entity

{

Key		Key

string		Url

string		Domain

string		ContentText

string		ContentHtml

Key		RiskyUrlKey

DateTime	DateCreated

bool?		IsDeleted

string		Locale

string?		Proxy

}
