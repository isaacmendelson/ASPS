# Reports

## מבנה
```
reports/
└── vX.X.X/
    ├── QSCORE.md          # ציון איכות (1-20)
    ├── CHANGELOG.md       # שינויים מהגרסה הקודמת
    ├── TEST-RESULTS.md    # תוצאות טסטים
    ├── screenshots/       # צילומי מסך
    │   ├── desktop.png
    │   ├── tablet.png
    │   └── mobile.png
    └── REVIEW.md          # סיכום סקירה
```

## QSCORE (Quality Score)

| קטגוריה | נקודות | מה בודקים |
|---------|--------|-----------|
| Code Quality | 1-5 | קריאות, סטנדרטים, DRY |
| Test Coverage | 1-5 | יחידות, אינטגרציה, edge cases |
| Performance | 1-5 | מהירות, זיכרון, סקיילביליות |
| Maintainability | 1-5 | תיעוד, מודולריות, פשטות |

**סף מעבר: 20/20** (מושלם בלבד)
