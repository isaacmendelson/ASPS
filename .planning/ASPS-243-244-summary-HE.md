# סיכום: מערכת Version Bump אוטומטית

**תאריך:** 17/03/2026  
**מתכנן:** Alex (CTO) 🧠  
**מיועד ל:** Yuri (Python Dev) 🐍

---

## 🎯 מה זה?

סקריפט Python שמנהל אוטומטית את מספרי הגרסאות בכל הפרויקט.

---

## ⚡ מה הסקריפט עושה?

1. **קורא** את הגרסה הנוכחית (0.0.0.1)
2. **מעלה** אותה ב-1 (0.0.0.2)
3. **מעדכן** 4 קבצים בבת אחת:
   - `ASPSBackend.csproj` (XML)
   - `WebApi.csproj` (XML)
   - `version.py` (Python)
   - `manifest.json` (JSON)
4. **מושך** רשימת commits מ-GitHub API
5. **יוצר** תיקייה `versions/0.0.0.2/readme.md` עם:
   - מספר גרסה
   - תאריך deploy
   - רשימת כל השינויים
6. **עושה commit** ו-tag אוטומטי ב-Git

---

## 📂 מבנה קבצים

```
asps/
├── scripts/
│   ├── version_bump.py          ← הסקריפט הראשי
│   ├── version_config.json      ← הגדרות (אילו קבצים לעדכן)
│   └── requirements.txt         ← תלויות Python
│
└── versions/                    ← היסטוריית גרסאות
    ├── 0.0.0.1/
    │   └── readme.md
    ├── 0.0.0.2/
    │   └── readme.md
    └── ...
```

---

## 🚀 איך משתמשים?

### התקנה (פעם אחת)
```bash
cd /root/.openclaw/workspace-ceo/asps
pip install -r scripts/requirements.txt
export GITHUB_TOKEN="your_token_here"
```

### שימוש יום-יומי
```bash
# הרצה רגילה - עולה ב-1 את הספרה האחרונה
python scripts/version_bump.py

# לראות מה יקרה בלי לשנות כלום (בדיקה)
python scripts/version_bump.py --dry-run

# לעלות ספרה אחרת
python scripts/version_bump.py --component minor   # 0.0.0.1 → 0.0.1.0
python scripts/version_bump.py --component major   # 0.0.0.1 → 0.1.0.0

# לשים גרסה ספציפית
python scripts/version_bump.py --set-version 1.0.0.0
```

---

## 🔧 לוגיקת הגרסאות

**פורמט:** `MAJOR2.MAJOR.MINOR.PATCH`

| פקודה | לפני | אחרי | מתי להשתמש? |
|-------|------|------|--------------|
| `--component patch` | 0.0.0.1 | 0.0.0.2 | תיקון באג קטן |
| `--component minor` | 0.0.0.1 | 0.0.1.0 | פיצ'ר חדש קטן |
| `--component major` | 0.0.0.1 | 0.1.0.0 | פיצ'ר גדול / שינוי משמעותי |
| `--component major2` | 0.0.0.1 | 1.0.0.0 | גרסה מלאה חדשה |

---

## 📝 איך הסקריפט מעדכן קבצים?

### XML (קבצי .csproj)
```xml
<PropertyGroup>
  <Version>0.0.0.2</Version>  ← מחפש ומעדכן את השורה הזו
</PropertyGroup>
```

### Python (version.py)
```python
VERSION = "0.0.0.2"  ← מחפש ומעדכן את השורה הזו
```

### JSON (manifest.json)
```json
{
  "version": "0.0.0.2"  ← מחפש ומעדכן את השדה הזה
}
```

---

## 🌐 GitHub API

הסקריפט מושך רשימת commits מ-GitHub API:

```python
GET https://api.github.com/repos/yehudaz136/asps/commits
Authorization: token YOUR_GITHUB_TOKEN
```

**חשוב:** צריך token עם הרשאת `repo:read`

---

## 📄 הקובץ readme.md שנוצר

```markdown
# Version 0.0.0.2

**Deployment Date:** 2026-03-17 15:30 UTC  
**Previous Version:** 0.0.0.1  

## 📦 Components Updated
- ✅ ASPSBackend
- ✅ WebApi
- ✅ Desktop App
- ✅ Chrome Extension

## 📝 Changes (25 commits)

#### [a1b2c3d] - Yuri
**Date:** 2026-03-17  
**Message:** Fixed bug in URL analyzer

#### [e4f5g6h] - Igor
**Date:** 2026-03-16  
**Message:** Added new endpoint for reports
...
```

---

## 🧪 בדיקות

### בדיקה מהירה (לא משנה כלום)
```bash
python scripts/version_bump.py --dry-run
```

**אמור להדפיס:**
```
🚀 ASPS Version Bump
==================================================
Current version: 0.0.0.1
New version: 0.0.0.2

[DRY RUN] Would update: ASPSBackend14_J/ASPSBackend/ASPSBackend.csproj
[DRY RUN] Would update: ASPSBackend14_J/WebApi/WebApi.csproj
[DRY RUN] Would update: apps/desktop/win/src/version.py
[DRY RUN] Would update: apps/extension/chrome/manifest.json
[DRY RUN] Would create: versions/0.0.0.2/readme.md
[DRY RUN] Would commit and tag: v0.0.0.2
```

### בדיקה שהקבצים עודכנו
```bash
grep -r "0.0.0.2" ASPSBackend14_J/ASPSBackend/ASPSBackend.csproj
grep -r "0.0.0.2" ASPSBackend14_J/WebApi/WebApi.csproj
grep -r "0.0.0.2" apps/desktop/win/src/version.py
grep -r "0.0.0.2" apps/extension/chrome/manifest.json
```

---

## 🔄 Integration עם CI/CD

אפשר להוסיף ל-GitHub Actions:

```yaml
# .github/workflows/deploy.yml
- name: Bump version
  env:
    GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
  run: python scripts/version_bump.py --component patch

- name: Push changes
  run: |
    git push origin main --follow-tags
```

---

## ⚠️ שגיאות נפוצות

| שגיאה | פתרון |
|-------|--------|
| `ModuleNotFoundError: requests` | `pip install -r scripts/requirements.txt` |
| `GITHUB_TOKEN not set` | `export GITHUB_TOKEN="..."` |
| `Version file not found` | לבדוק ש-path בקונפיג נכון |
| `Invalid version format` | גרסה חייבת להיות X.X.X.X |

---

## 📋 משימות ליישום (ASPS-244)

**מפתח:** Yuri 🐍  
**זמן משוער:** 2 ימים

### יום 1
- [ ] יצירת structure קבצים
- [ ] יישום version_config.json
- [ ] יישום version_bump.py (פונקציות core)
- [ ] יישום עדכון הקבצים (XML, Python, JSON)
- [ ] בדיקות ראשוניות

### יום 2
- [ ] יישום GitHub API integration
- [ ] יישום יצירת README
- [ ] יישום Git operations
- [ ] כתיבת unit tests
- [ ] בדיקות מלאות
- [ ] תיעוד
- [ ] ✅ Ready for QA

---

## 📚 מסמכים נוספים

1. **תכנון טכני מלא (אנגלית):**  
   `/root/.openclaw/workspace-ceo/asps/.planning/ASPS-243-version-bump-design.md`

2. **מדריך יישום מפורט (אנגלית):**  
   `/root/.openclaw/workspace-ceo/asps/.planning/ASPS-244-implementation-guide.md`

---

## 🎓 למה Python ולא Bash/PowerShell?

✅ **Python:**
- עובד על כל מערכות הפעלה (Windows, Linux, macOS)
- קל לעבוד עם JSON, XML, text files
- יש ספריות מוכנות ל-GitHub API
- קל לכתוב tests
- הצוות מכיר

❌ **Bash:**
- לא עובד טוב על Windows
- קשה לעבוד עם XML/JSON

❌ **PowerShell:**
- צריך PowerShell Core על Linux
- less familiar לצוות

---

## 💡 טיפים

1. **תמיד תריץ `--dry-run` לפני הרצה אמיתית**
2. **תשמור backup לפני שרצים בפעם הראשונה**
3. **תוודא ש-GITHUB_TOKEN בסביבה**
4. **תבדוק שכל הקבצים קיימים לפני הרצה**

---

## ❓ שאלות?

**לפני שמתחיל** - קרא את המדריך המפורט:
`ASPS-244-implementation-guide.md`

**בעיות בדרך** - תתייעץ עם:
1. Alex (CTO) 🧠
2. טומי (CEO) אם צריך approval
3. Isaac אם צריך context נוסף

---

**בהצלחה! 🐍 אתה יכול לעשות את זה!**
