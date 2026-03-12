---
title: "Getting Started - ASPS"
version: 1.0.0
last_updated: 2026-03-12
author: Zappa (CEO)
---

# ברוכים הבאים ל-ASPS! 🚀

## מה זה ASPS?

**Anti-Scam Protection System** - מערכת להגנה מפני הונאות אונליין.

המערכת מנתחת:
- URLs חשודים
- אתרי פישינג
- גישה מרחוק לא מורשית
- התנהגות חשודה

---

## הצוות

| תפקיד | שם | אחריות |
|-------|-----|---------|
| CEO | Zappa | ניהול כללי |
| CTO | Alex | ארכיטקטורה וטכנולוגיה |
| Backend | Igor, Dmitri, Anna, Tal | פיתוח שרת |
| Frontend | Maya | פיתוח ממשק |
| Python | Yuri | Agent & Extension |
| QA | QA | בדיקות ואיכות |
| Security | Shadow | אבטחה |
| AI | Dr. Nova | Data & AI |

---

## סביבת העבודה

### Repository
```
github.com/yehudaz136/asps
Branch: zappa_dev_1
```

### מבנה הפרויקט
```
asps/
├── ASPSBackend14_J/
│   ├── Common/          # Entities, Models, Enums
│   ├── Interface/       # Interfaces & Contracts
│   ├── Business/        # Business Logic, Services
│   ├── WebApi/          # REST API, Controllers
│   ├── ASPSBackend/     # Background Services
│   └── ASPS.Tests/      # Unit Tests
├── AIDucation/          # מדריכים (אתה כאן!)
└── ...
```

### טכנולוגיות
- **Backend:** C# / .NET 8 / ASP.NET Core
- **Database:** MySQL / Entity Framework Core
- **Messaging:** Akka.NET (Actors), NetMQ
- **Testing:** xUnit, Moq, FluentAssertions
- **Agent:** Python

---

## JIRA

כל המשימות מנוהלות ב-JIRA.

- **URL:** http://187.124.10.197:8080
- **Project:** ASPS

### סטטוסים
```
To Do → In Progress → Done
```

### חובות:
1. עדכן `assignee` לעצמך
2. עדכן סטטוס כשמתחיל/מסיים
3. הוסף comment עם מה עשית

---

## כלל הזהב 🚨

> **לא ברור? שואלים!**
>
> 1. קודם לאלכס (CTO)
> 2. אם הוא לא יודע → טומי (CEO)
> 3. אם גם הוא לא יודע → Isaac
>
> **אסור להניח. אסור לנחש.**

---

## צעדים ראשונים

### 1. Clone הפרויקט
```bash
git clone https://github.com/yehudaz136/asps.git
cd asps
git checkout zappa_dev_1
```

### 2. Build
```bash
cd ASPSBackend14_J
dotnet restore
dotnet build
```

### 3. Run Tests
```bash
dotnet test
```

### 4. קרא את המדריכים
- [Unit Testing Guide](../guides/unit-testing.md)
- [Coding Standards](../guides/coding-standards.md)

### 5. קבל משימה מאלכס
- היכנס ל-JIRA
- מצא משימה עם ה-assignee שלך
- התחל לעבוד!

---

## שאלות נפוצות

### איפה הקוד של X?
```bash
find . -name "*.cs" | xargs grep -l "ClassName"
```

### איך מריצים את השרת?
```bash
cd WebApi
dotnet run
```

### איך עושים migration?
```bash
cd Business
dotnet ef migrations add MigrationName
dotnet ef database update
```

---

## בהצלחה! 🎉

יש לך את כל הכלים להצליח. 
אם צריך עזרה - **שאל!**
