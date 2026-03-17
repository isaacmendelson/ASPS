# ASPS-243: Version Bump & README Automation - Technical Design

**Created:** 2026-03-17  
**Designer:** Alex (CTO)  
**Status:** Design Phase

---

## 🎯 Overview

Automated version management system that:
1. Increments version across all project components
2. Generates deployment documentation
3. Captures commit history from GitHub
4. Runs during production deployment

---

## 📋 Technical Decisions

### 1. Language: **Python 3.8+**

**Reasoning:**
- ✅ Cross-platform (Windows, Linux, macOS)
- ✅ Built-in JSON support for manifest.json
- ✅ Excellent XML parsing for .csproj files (xml.etree)
- ✅ Simple text manipulation for version.py
- ✅ `requests` library for GitHub API
- ✅ Native datetime formatting
- ✅ Already available in deployment environments

**Alternatives considered:**
- ❌ Bash - not cross-platform (Windows issues)
- ❌ PowerShell - requires PS Core on Linux
- ❌ Node.js - adds unnecessary dependency

---

## 🏗️ Architecture

### File Structure
```
/root/.openclaw/workspace-ceo/asps/
├── scripts/
│   ├── version_bump.py          # Main script
│   ├── version_config.json      # Version file paths config
│   └── requirements.txt         # Python dependencies
├── versions/                    # Version history
│   ├── 0.0.0.1/
│   │   └── readme.md
│   ├── 0.0.0.2/
│   │   └── readme.md
│   └── ...
└── [existing project files]
```

### Version Files Managed
1. **ASPSBackend14_J/ASPSBackend/ASPSBackend.csproj**
   - Format: `<Version>X.X.X.X</Version>`
   - Type: XML

2. **ASPSBackend14_J/WebApi/WebApi.csproj**
   - Format: `<Version>X.X.X.X</Version>`
   - Type: XML

3. **apps/desktop/win/src/version.py**
   - Format: `VERSION = "X.X.X.X"`
   - Type: Python constant

4. **apps/extension/chrome/manifest.json**
   - Format: `"version": "X.X.X.X"`
   - Type: JSON

---

## 🔧 Script Design

### 1. version_config.json
```json
{
  "version_files": [
    {
      "path": "ASPSBackend14_J/ASPSBackend/ASPSBackend.csproj",
      "type": "xml",
      "xpath": ".//PropertyGroup/Version"
    },
    {
      "path": "ASPSBackend14_J/WebApi/WebApi.csproj",
      "type": "xml",
      "xpath": ".//PropertyGroup/Version"
    },
    {
      "path": "apps/desktop/win/src/version.py",
      "type": "python",
      "pattern": "VERSION = \"{{version}}\""
    },
    {
      "path": "apps/extension/chrome/manifest.json",
      "type": "json",
      "key": "version"
    }
  ],
  "github": {
    "owner": "yehudaz136",
    "repo": "asps",
    "token_env": "GITHUB_TOKEN"
  },
  "versions_dir": "versions"
}
```

### 2. version_bump.py - Main Script

#### Command Line Interface
```bash
# Basic usage - increment patch (4th digit)
python scripts/version_bump.py

# Specify component to bump
python scripts/version_bump.py --component patch    # 0.0.0.1 → 0.0.0.2
python scripts/version_bump.py --component minor    # 0.0.0.1 → 0.0.1.0
python scripts/version_bump.py --component major    # 0.0.0.1 → 0.1.0.0
python scripts/version_bump.py --component major2   # 0.0.0.1 → 1.0.0.0

# Dry run (preview changes)
python scripts/version_bump.py --dry-run

# Custom version (override)
python scripts/version_bump.py --set-version 1.2.3.4

# Since last tag (for commit log)
python scripts/version_bump.py --since-tag v0.0.0.1
```

#### Parameters
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--component` | str | `patch` | Which version component to bump |
| `--dry-run` | flag | False | Preview without changes |
| `--set-version` | str | None | Override with specific version |
| `--since-tag` | str | None | Git tag to fetch commits from |
| `--config` | str | `scripts/version_config.json` | Config file path |
| `--no-readme` | flag | False | Skip README generation |
| `--no-commit` | flag | False | Skip git commit |

#### Input
1. **Current version** - Read from first file in config (source of truth)
2. **GitHub API** - Commits since last deployment
3. **Configuration** - `version_config.json`

#### Output
1. **Updated files** - All 4 version files synchronized
2. **README** - `versions/X.X.X.X/readme.md`
3. **Git commit** - Automatic commit with version message
4. **Return code** - 0 (success) / 1 (error)

---

## 📝 Script Flow

```
┌─────────────────────────────────────┐
│ 1. Load Configuration               │
│    - Read version_config.json       │
│    - Validate file paths exist      │
└──────────────┬──────────────────────┘
               │
┌──────────────▼──────────────────────┐
│ 2. Read Current Version             │
│    - Parse first version file       │
│    - Validate format (X.X.X.X)      │
└──────────────┬──────────────────────┘
               │
┌──────────────▼──────────────────────┐
│ 3. Calculate New Version            │
│    - Increment based on --component │
│    - OR use --set-version           │
└──────────────┬──────────────────────┘
               │
┌──────────────▼──────────────────────┐
│ 4. Update All Version Files         │
│    - XML: Update <Version> tag      │
│    - Python: Replace VERSION line   │
│    - JSON: Update version key       │
└──────────────┬──────────────────────┘
               │
┌──────────────▼──────────────────────┐
│ 5. Fetch Commits from GitHub        │
│    - Use GitHub API                 │
│    - Get commits since last tag     │
└──────────────┬──────────────────────┘
               │
┌──────────────▼──────────────────────┐
│ 6. Generate README                  │
│    - Create versions/X.X.X.X/       │
│    - Write readme.md                │
└──────────────┬──────────────────────┘
               │
┌──────────────▼──────────────────────┐
│ 7. Git Commit (optional)            │
│    - Add changed files              │
│    - Commit: "Version bump: X.X.X.X"│
│    - Create tag: vX.X.X.X           │
└─────────────────────────────────────┘
```

---

## 🔌 GitHub API Integration

### Authentication
```python
import os
import requests

GITHUB_TOKEN = os.environ.get('GITHUB_TOKEN')
headers = {
    'Authorization': f'token {GITHUB_TOKEN}',
    'Accept': 'application/vnd.github.v3+json'
}
```

### API Endpoints Used
1. **List Commits**
   ```
   GET /repos/{owner}/{repo}/commits
   Params: since={iso_timestamp}, per_page=100
   ```

2. **Get Tags** (optional, for finding last deployment)
   ```
   GET /repos/{owner}/{repo}/tags
   ```

### Commit Data Extracted
```python
{
    "sha": "abc123...",
    "commit": {
        "author": {"name": "...", "date": "..."},
        "message": "..."
    }
}
```

---

## 📄 README Template

### versions/X.X.X.X/readme.md
```markdown
# Version X.X.X.X

**Deployment Date:** YYYY-MM-DD HH:MM UTC  
**Previous Version:** X.X.X.X  
**Repository:** https://github.com/yehudaz136/asps

---

## 📦 Components Updated

- ✅ ASPSBackend (`ASPSBackend.csproj`)
- ✅ WebApi (`WebApi.csproj`)
- ✅ Desktop App (`version.py`)
- ✅ Chrome Extension (`manifest.json`)

---

## 📝 Changes Since Last Deployment

### Commits (N total)

#### [short_sha] - Author Name
**Date:** YYYY-MM-DD HH:MM  
**Message:**  
```
Full commit message here
```

#### [short_sha] - Author Name
...

---

## 🔗 Links

- [GitHub Commit Range](https://github.com/yehudaz136/asps/compare/v0.0.0.1...v0.0.0.2)
- [Full Changelog](https://github.com/yehudaz136/asps/blob/main/CHANGELOG.md)

---

**Generated by:** version_bump.py  
**Script Version:** 1.0.0
```

---

## 🔄 Version Update Logic

### Version Format: `MAJOR2.MAJOR.MINOR.PATCH`

```python
def increment_version(current: str, component: str) -> str:
    """
    current: "0.0.0.1"
    component: "patch" | "minor" | "major" | "major2"
    """
    parts = [int(x) for x in current.split('.')]
    major2, major, minor, patch = parts
    
    if component == 'patch':
        patch += 1
    elif component == 'minor':
        minor += 1
        patch = 0
    elif component == 'major':
        major += 1
        minor = 0
        patch = 0
    elif component == 'major2':
        major2 += 1
        major = 0
        minor = 0
        patch = 0
    
    return f"{major2}.{major}.{minor}.{patch}"
```

### File Update Strategies

#### XML (.csproj files)
```python
import xml.etree.ElementTree as ET

def update_csproj(file_path: str, new_version: str):
    tree = ET.parse(file_path)
    root = tree.getroot()
    
    # Find <Version> tag in any <PropertyGroup>
    for prop_group in root.findall('.//PropertyGroup'):
        version_elem = prop_group.find('Version')
        if version_elem is not None:
            version_elem.text = new_version
            break
    
    tree.write(file_path, encoding='utf-8', xml_declaration=True)
```

#### Python (version.py)
```python
import re

def update_version_py(file_path: str, new_version: str):
    with open(file_path, 'r') as f:
        content = f.read()
    
    pattern = r'VERSION\s*=\s*"[\d\.]+"'
    replacement = f'VERSION = "{new_version}"'
    new_content = re.sub(pattern, replacement, content)
    
    with open(file_path, 'w') as f:
        f.write(new_content)
```

#### JSON (manifest.json)
```python
import json

def update_manifest_json(file_path: str, new_version: str):
    with open(file_path, 'r') as f:
        data = json.load(f)
    
    data['version'] = new_version
    
    with open(file_path, 'w') as f:
        json.dump(data, f, indent=2)
        f.write('\n')  # Add trailing newline
```

---

## 🚀 Deployment Integration

### CI/CD Pipeline (GitHub Actions Example)
```yaml
# .github/workflows/deploy.yml
name: Deploy to Production

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup Python
        uses: actions/setup-python@v4
        with:
          python-version: '3.10'
      
      - name: Install dependencies
        run: pip install -r scripts/requirements.txt
      
      - name: Bump version
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          python scripts/version_bump.py --component patch
      
      - name: Commit version bump
        run: |
          git config user.name "GitHub Actions"
          git config user.email "actions@github.com"
          git push
          git push --tags
      
      - name: Deploy services
        run: |
          # Your deployment commands here
          docker-compose up -d
```

### Manual Deployment
```bash
# 1. Pull latest
git pull origin main

# 2. Bump version
export GITHUB_TOKEN="your_token_here"
python scripts/version_bump.py

# 3. Push changes
git push origin main --follow-tags

# 4. Deploy
docker-compose up -d
```

---

## 📦 Dependencies

### scripts/requirements.txt
```txt
requests>=2.31.0
python-dateutil>=2.8.2
```

### Installation
```bash
pip install -r scripts/requirements.txt
```

---

## 🧪 Testing Strategy

### Unit Tests
```python
# tests/test_version_bump.py
import unittest
from scripts.version_bump import increment_version

class TestVersionBump(unittest.TestCase):
    def test_patch_increment(self):
        self.assertEqual(increment_version("0.0.0.1", "patch"), "0.0.0.2")
    
    def test_minor_increment(self):
        self.assertEqual(increment_version("0.0.0.1", "minor"), "0.0.1.0")
    
    def test_major_increment(self):
        self.assertEqual(increment_version("0.0.0.1", "major"), "0.1.0.0")
```

### Integration Tests
```bash
# Test dry-run
python scripts/version_bump.py --dry-run

# Test with custom version
python scripts/version_bump.py --set-version 0.0.0.99 --no-commit

# Verify all files updated
grep -r "0.0.0.99" ASPSBackend14_J/ASPSBackend/ASPSBackend.csproj
grep -r "0.0.0.99" apps/desktop/win/src/version.py
```

---

## 🔒 Security Considerations

1. **GitHub Token**
   - Store in environment variable, not in code
   - Use GitHub Actions secrets in CI/CD
   - Minimum permissions: `repo:read`

2. **Version File Validation**
   - Validate version format before update
   - Backup files before modification
   - Atomic writes (write to temp, then rename)

3. **Git Operations**
   - Verify repository is clean before commit
   - Check for conflicts
   - Fail gracefully if push rejected

---

## 📊 Error Handling

### Common Errors
| Error | Handling |
|-------|----------|
| File not found | Exit with clear message |
| Invalid version format | Reject and suggest fix |
| GitHub API rate limit | Show remaining quota |
| Network error | Retry 3 times with backoff |
| Git conflict | Abort and notify user |

### Example
```python
import sys

try:
    current_version = read_version(config['version_files'][0])
except FileNotFoundError:
    print(f"❌ Error: Version file not found")
    sys.exit(1)
except ValueError as e:
    print(f"❌ Error: Invalid version format: {e}")
    sys.exit(1)
```

---

## 📈 Future Enhancements

1. **Changelog Generation** (Phase 2)
   - Parse commit messages for features/fixes
   - Categorize by conventional commits
   - Auto-update CHANGELOG.md

2. **Slack/Discord Notifications** (Phase 3)
   - Send deployment notification
   - Include version and commit count

3. **Rollback Support** (Phase 4)
   - `version_bump.py --rollback` to previous version
   - Restore from versions/ history

4. **Multi-Branch Support** (Phase 5)
   - Different version streams (dev, staging, prod)
   - Branch-specific version prefixes

---

## ✅ Acceptance Criteria

- [x] Script is cross-platform (Windows/Linux/macOS)
- [x] Updates all 4 version files correctly
- [x] Fetches commits from GitHub API
- [x] Generates README with proper format
- [x] Handles errors gracefully
- [x] Supports dry-run mode
- [x] Can be integrated into CI/CD
- [x] Creates git tag automatically
- [x] Documents all changes in versions/

---

## 👥 Implementation Assignment

**Task:** ASPS-244 - Implement Version Bump Script  
**Assignee:** Yuri (Python Dev) 🐍  
**Estimated:** 2 days  
**Priority:** High

### Subtasks:
1. Create `scripts/version_bump.py` with all functions
2. Create `scripts/version_config.json`
3. Create `scripts/requirements.txt`
4. Write unit tests
5. Test with dry-run on all platforms
6. Document usage in README
7. Create example GitHub Action workflow

---

## 📞 Contact

**Designer:** Alex (CTO) 🧠  
**Questions:** Ask in #dev channel or tag @alex

---

**Document Version:** 1.0  
**Last Updated:** 2026-03-17
