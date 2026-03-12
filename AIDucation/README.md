# AIDucation 📚

מדריכים והדרכות לעובדים חדשים ב-ASPS.

## מבנה התיקייה

```
AIDucation/
├── README.md                    # קובץ זה
├── CHANGELOG.md                 # היסטוריית שינויים
├── guides/                      # מדריכים טכניים
│   ├── unit-testing.md         # מדריך כתיבת Unit Tests
│   ├── coding-standards.md     # סטנדרטים לכתיבת קוד
│   └── ...
├── onboarding/                  # הדרכת עובדים חדשים
│   ├── getting-started.md      # צעדים ראשונים
│   ├── architecture.md         # הכרת הארכיטקטורה
│   └── ...
└── templates/                   # תבניות
    ├── test-template.cs        # תבנית לקובץ טסט
    └── ...
```

## ניהול גירסאות

כל מדריך כולל:
- **Header** עם גירסה, תאריך עדכון, ומחבר
- **שינויים** מתועדים ב-CHANGELOG.md

### פורמט גירסה
```
v{MAJOR}.{MINOR}.{PATCH}

MAJOR - שינוי מהותי במדריך
MINOR - הוספת תוכן חדש
PATCH - תיקוני שגיאות/ניסוח
```

### דוגמה לכותרת מדריך
```markdown
---
title: "Unit Testing Guide"
version: 1.0.0
last_updated: 2026-03-12
author: QA Team
---
```

## איך להוסיף מדריך חדש

1. צור קובץ `.md` בתיקייה המתאימה
2. הוסף header עם גירסה
3. עדכן את CHANGELOG.md
4. עדכן את README.md אם צריך
