# ASPS-244: Version Bump Script - Implementation Guide

**Developer:** Yuri (Python Dev) 🐍  
**Estimated Time:** 2 days  
**Status:** Ready for Development

---

## 🎯 Goal

Implement the version bump automation script based on [ASPS-243 Design](./ASPS-243-version-bump-design.md).

---

## 📋 Checklist

### Day 1: Core Implementation
- [ ] Create directory structure
- [ ] Implement `version_config.json`
- [ ] Implement `version_bump.py` - core functions
- [ ] Implement file updaters (XML, Python, JSON)
- [ ] Implement version increment logic
- [ ] Test basic functionality

### Day 2: Integration & Testing
- [ ] Implement GitHub API integration
- [ ] Implement README generation
- [ ] Implement git operations
- [ ] Write unit tests
- [ ] Test on different platforms
- [ ] Create documentation
- [ ] QA ready

---

## 📁 Step 1: Directory Structure (10 min)

```bash
cd /root/.openclaw/workspace-ceo/asps

# Create directories
mkdir -p scripts
mkdir -p versions
mkdir -p tests

# Create files
touch scripts/version_bump.py
touch scripts/version_config.json
touch scripts/requirements.txt
touch tests/test_version_bump.py
```

---

## 📝 Step 2: Configuration File (15 min)

Create `scripts/version_config.json`:

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
  "versions_dir": "versions",
  "git_auto_commit": true,
  "git_auto_tag": true
}
```

---

## 🐍 Step 3: Main Script Structure (30 min)

Create `scripts/version_bump.py` with this skeleton:

```python
#!/usr/bin/env python3
"""
ASPS Version Bump Automation
Increments version across all project components and generates deployment docs.
"""

import argparse
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, List, Optional, Tuple

try:
    import requests
except ImportError:
    print("❌ Error: 'requests' library not found. Install: pip install -r scripts/requirements.txt")
    sys.exit(1)

# Constants
SCRIPT_VERSION = "1.0.0"
CONFIG_DEFAULT = "scripts/version_config.json"


class VersionBumper:
    """Handles version bumping across multiple file types."""
    
    def __init__(self, config_path: str, dry_run: bool = False):
        self.config_path = config_path
        self.dry_run = dry_run
        self.config = self._load_config()
        self.repo_root = Path(__file__).parent.parent.absolute()
        
    def _load_config(self) -> Dict:
        """Load and validate configuration file."""
        pass  # TODO: Implement
    
    def get_current_version(self) -> str:
        """Read current version from first version file."""
        pass  # TODO: Implement
    
    def increment_version(self, current: str, component: str) -> str:
        """Calculate new version based on component."""
        pass  # TODO: Implement
    
    def update_all_files(self, new_version: str) -> None:
        """Update version in all configured files."""
        pass  # TODO: Implement
    
    def fetch_commits(self, since_tag: Optional[str] = None) -> List[Dict]:
        """Fetch commits from GitHub API."""
        pass  # TODO: Implement
    
    def generate_readme(self, version: str, commits: List[Dict]) -> None:
        """Generate version README file."""
        pass  # TODO: Implement
    
    def git_commit_and_tag(self, version: str) -> None:
        """Commit changes and create git tag."""
        pass  # TODO: Implement


def main():
    """Main entry point."""
    parser = argparse.ArgumentParser(
        description="ASPS Version Bump Automation",
        formatter_class=argparse.RawDescriptionHelpFormatter
    )
    
    parser.add_argument(
        '--component',
        choices=['patch', 'minor', 'major', 'major2'],
        default='patch',
        help='Version component to bump (default: patch)'
    )
    
    parser.add_argument(
        '--set-version',
        type=str,
        help='Set specific version (overrides --component)'
    )
    
    parser.add_argument(
        '--dry-run',
        action='store_true',
        help='Preview changes without applying them'
    )
    
    parser.add_argument(
        '--since-tag',
        type=str,
        help='Git tag to fetch commits from (e.g., v0.0.0.1)'
    )
    
    parser.add_argument(
        '--config',
        type=str,
        default=CONFIG_DEFAULT,
        help=f'Configuration file path (default: {CONFIG_DEFAULT})'
    )
    
    parser.add_argument(
        '--no-readme',
        action='store_true',
        help='Skip README generation'
    )
    
    parser.add_argument(
        '--no-commit',
        action='store_true',
        help='Skip git commit and tag'
    )
    
    args = parser.parse_args()
    
    # TODO: Implement main logic
    print("🚀 ASPS Version Bump")
    print("=" * 50)
    

if __name__ == '__main__':
    main()
```

---

## 🔧 Step 4: Implement Core Functions (2 hours)

### 4.1: Load Configuration

```python
def _load_config(self) -> Dict:
    """Load and validate configuration file."""
    try:
        with open(self.config_path, 'r') as f:
            config = json.load(f)
        
        # Validate required keys
        required_keys = ['version_files', 'github', 'versions_dir']
        for key in required_keys:
            if key not in config:
                raise ValueError(f"Missing required key: {key}")
        
        return config
    except FileNotFoundError:
        print(f"❌ Error: Config file not found: {self.config_path}")
        sys.exit(1)
    except json.JSONDecodeError as e:
        print(f"❌ Error: Invalid JSON in config file: {e}")
        sys.exit(1)
```

### 4.2: Read Current Version

```python
def get_current_version(self) -> str:
    """Read current version from first version file."""
    first_file = self.config['version_files'][0]
    file_path = self.repo_root / first_file['path']
    
    if not file_path.exists():
        print(f"❌ Error: Version file not found: {file_path}")
        sys.exit(1)
    
    file_type = first_file['type']
    
    if file_type == 'xml':
        tree = ET.parse(file_path)
        root = tree.getroot()
        version_elem = root.find(first_file['xpath'])
        if version_elem is None:
            raise ValueError("Version element not found in XML")
        version = version_elem.text
        
    elif file_type == 'python':
        with open(file_path, 'r') as f:
            content = f.read()
        match = re.search(r'VERSION\s*=\s*"([\d\.]+)"', content)
        if not match:
            raise ValueError("VERSION not found in Python file")
        version = match.group(1)
        
    elif file_type == 'json':
        with open(file_path, 'r') as f:
            data = json.load(f)
        version = data.get(first_file['key'])
        if not version:
            raise ValueError(f"Key '{first_file['key']}' not found in JSON")
    
    else:
        raise ValueError(f"Unsupported file type: {file_type}")
    
    # Validate version format
    if not re.match(r'^\d+\.\d+\.\d+\.\d+$', version):
        raise ValueError(f"Invalid version format: {version} (expected X.X.X.X)")
    
    return version
```

### 4.3: Increment Version

```python
def increment_version(self, current: str, component: str) -> str:
    """Calculate new version based on component."""
    parts = [int(x) for x in current.split('.')]
    
    if len(parts) != 4:
        raise ValueError(f"Invalid version format: {current}")
    
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
    else:
        raise ValueError(f"Invalid component: {component}")
    
    return f"{major2}.{major}.{minor}.{patch}"
```

### 4.4: Update Files

```python
def update_all_files(self, new_version: str) -> None:
    """Update version in all configured files."""
    for file_config in self.config['version_files']:
        file_path = self.repo_root / file_config['path']
        file_type = file_config['type']
        
        if self.dry_run:
            print(f"  [DRY RUN] Would update: {file_config['path']}")
            continue
        
        print(f"  Updating: {file_config['path']}")
        
        if file_type == 'xml':
            self._update_xml_file(file_path, file_config['xpath'], new_version)
        elif file_type == 'python':
            self._update_python_file(file_path, new_version)
        elif file_type == 'json':
            self._update_json_file(file_path, file_config['key'], new_version)

def _update_xml_file(self, file_path: Path, xpath: str, new_version: str) -> None:
    """Update version in XML file (.csproj)."""
    tree = ET.parse(file_path)
    root = tree.getroot()
    
    version_elem = root.find(xpath)
    if version_elem is not None:
        version_elem.text = new_version
    
    tree.write(file_path, encoding='utf-8', xml_declaration=True)

def _update_python_file(self, file_path: Path, new_version: str) -> None:
    """Update VERSION constant in Python file."""
    with open(file_path, 'r') as f:
        content = f.read()
    
    pattern = r'VERSION\s*=\s*"[\d\.]+"'
    replacement = f'VERSION = "{new_version}"'
    new_content = re.sub(pattern, replacement, content)
    
    with open(file_path, 'w') as f:
        f.write(new_content)

def _update_json_file(self, file_path: Path, key: str, new_version: str) -> None:
    """Update version in JSON file (manifest.json)."""
    with open(file_path, 'r') as f:
        data = json.load(f)
    
    data[key] = new_version
    
    with open(file_path, 'w') as f:
        json.dump(data, f, indent=2)
        f.write('\n')  # Add trailing newline
```

---

## 🌐 Step 5: GitHub API Integration (1 hour)

```python
def fetch_commits(self, since_tag: Optional[str] = None) -> List[Dict]:
    """Fetch commits from GitHub API."""
    github_config = self.config['github']
    owner = github_config['owner']
    repo = github_config['repo']
    
    # Get GitHub token
    token_env = github_config.get('token_env', 'GITHUB_TOKEN')
    token = os.environ.get(token_env)
    
    if not token:
        print(f"⚠️  Warning: {token_env} not set. Skipping commit fetch.")
        return []
    
    headers = {
        'Authorization': f'token {token}',
        'Accept': 'application/vnd.github.v3+json'
    }
    
    # Determine since parameter
    params = {'per_page': 100}
    
    if since_tag:
        # Get tag date
        tag_url = f"https://api.github.com/repos/{owner}/{repo}/git/refs/tags/{since_tag}"
        try:
            tag_response = requests.get(tag_url, headers=headers, timeout=10)
            tag_response.raise_for_status()
            # ... get commit date from tag
        except Exception as e:
            print(f"⚠️  Warning: Could not fetch tag {since_tag}: {e}")
    
    # Fetch commits
    url = f"https://api.github.com/repos/{owner}/{repo}/commits"
    
    try:
        response = requests.get(url, headers=headers, params=params, timeout=30)
        response.raise_for_status()
        
        commits = response.json()
        print(f"  Fetched {len(commits)} commits from GitHub")
        return commits
        
    except requests.exceptions.RequestException as e:
        print(f"⚠️  Warning: Could not fetch commits: {e}")
        return []
```

---

## 📄 Step 6: README Generation (45 min)

```python
def generate_readme(self, version: str, commits: List[Dict], prev_version: str) -> None:
    """Generate version README file."""
    versions_dir = self.repo_root / self.config['versions_dir']
    version_dir = versions_dir / version
    
    if self.dry_run:
        print(f"  [DRY RUN] Would create: {version_dir}/readme.md")
        return
    
    # Create directory
    version_dir.mkdir(parents=True, exist_ok=True)
    
    # Generate README content
    now = datetime.now(timezone.utc)
    
    readme_content = f"""# Version {version}

**Deployment Date:** {now.strftime('%Y-%m-%d %H:%M UTC')}  
**Previous Version:** {prev_version}  
**Repository:** https://github.com/{self.config['github']['owner']}/{self.config['github']['repo']}

---

## 📦 Components Updated

- ✅ ASPSBackend (`ASPSBackend.csproj`)
- ✅ WebApi (`WebApi.csproj`)
- ✅ Desktop App (`version.py`)
- ✅ Chrome Extension (`manifest.json`)

---

## 📝 Changes Since Last Deployment

### Commits ({len(commits)} total)

"""
    
    # Add commits
    for commit in commits[:50]:  # Limit to 50 most recent
        sha_short = commit['sha'][:7]
        author = commit['commit']['author']['name']
        date = commit['commit']['author']['date']
        message = commit['commit']['message'].split('\n')[0]  # First line only
        
        readme_content += f"""#### [{sha_short}] - {author}
**Date:** {date}  
**Message:** {message}

"""
    
    readme_content += f"""
---

## 🔗 Links

- [GitHub Commit Range](https://github.com/{self.config['github']['owner']}/{self.config['github']['repo']}/compare/v{prev_version}...v{version})
- [Full Repository](https://github.com/{self.config['github']['owner']}/{self.config['github']['repo']})

---

**Generated by:** version_bump.py v{SCRIPT_VERSION}  
**Generated at:** {now.strftime('%Y-%m-%d %H:%M:%S UTC')}
"""
    
    # Write file
    readme_path = version_dir / 'readme.md'
    with open(readme_path, 'w') as f:
        f.write(readme_content)
    
    print(f"  ✅ Created: {readme_path}")
```

---

## 🔄 Step 7: Git Operations (30 min)

```python
import subprocess

def git_commit_and_tag(self, version: str) -> None:
    """Commit changes and create git tag."""
    if self.dry_run:
        print(f"  [DRY RUN] Would commit and tag: v{version}")
        return
    
    if not self.config.get('git_auto_commit', True):
        print("  Skipping git commit (disabled in config)")
        return
    
    try:
        # Add files
        subprocess.run(['git', 'add', '-A'], check=True, cwd=self.repo_root)
        
        # Commit
        commit_msg = f"Version bump: {version}"
        subprocess.run(
            ['git', 'commit', '-m', commit_msg],
            check=True,
            cwd=self.repo_root
        )
        print(f"  ✅ Committed: {commit_msg}")
        
        # Create tag
        if self.config.get('git_auto_tag', True):
            tag_name = f"v{version}"
            subprocess.run(
                ['git', 'tag', '-a', tag_name, '-m', f"Release {version}"],
                check=True,
                cwd=self.repo_root
            )
            print(f"  ✅ Created tag: {tag_name}")
        
    except subprocess.CalledProcessError as e:
        print(f"⚠️  Warning: Git operation failed: {e}")
```

---

## 🧪 Step 8: Testing (1.5 hours)

### 8.1: Unit Tests

Create `tests/test_version_bump.py`:

```python
import unittest
import sys
from pathlib import Path

# Add scripts to path
sys.path.insert(0, str(Path(__file__).parent.parent / 'scripts'))

from version_bump import VersionBumper


class TestVersionIncrement(unittest.TestCase):
    """Test version increment logic."""
    
    def setUp(self):
        # Create a mock bumper (without loading config)
        self.bumper = VersionBumper.__new__(VersionBumper)
    
    def test_patch_increment(self):
        result = self.bumper.increment_version("0.0.0.1", "patch")
        self.assertEqual(result, "0.0.0.2")
    
    def test_minor_increment(self):
        result = self.bumper.increment_version("0.0.0.1", "minor")
        self.assertEqual(result, "0.0.1.0")
    
    def test_major_increment(self):
        result = self.bumper.increment_version("0.0.0.1", "major")
        self.assertEqual(result, "0.1.0.0")
    
    def test_major2_increment(self):
        result = self.bumper.increment_version("0.0.0.1", "major2")
        self.assertEqual(result, "1.0.0.0")
    
    def test_multiple_increments(self):
        result = self.bumper.increment_version("1.2.3.4", "patch")
        self.assertEqual(result, "1.2.3.5")


if __name__ == '__main__':
    unittest.main()
```

### 8.2: Integration Test

```bash
# Test dry-run
cd /root/.openclaw/workspace-ceo/asps
python scripts/version_bump.py --dry-run

# Expected output:
# 🚀 ASPS Version Bump
# ==================================================
# Current version: 0.0.0.1
# New version: 0.0.0.2
# [DRY RUN] Would update: ASPSBackend14_J/ASPSBackend/ASPSBackend.csproj
# ...
```

### 8.3: Manual Testing Checklist

```bash
# 1. Test basic increment
python scripts/version_bump.py --dry-run

# 2. Test custom version
python scripts/version_bump.py --set-version 0.0.0.99 --dry-run

# 3. Test minor increment
python scripts/version_bump.py --component minor --dry-run

# 4. Test without GitHub token (should warn but continue)
unset GITHUB_TOKEN
python scripts/version_bump.py --dry-run

# 5. Test actual run (after verifying dry-run works)
export GITHUB_TOKEN="your_token"
python scripts/version_bump.py --no-commit

# 6. Verify all files updated
grep -r "0.0.0.2" ASPSBackend14_J/ASPSBackend/ASPSBackend.csproj
grep -r "0.0.0.2" ASPSBackend14_J/WebApi/WebApi.csproj
grep -r "0.0.0.2" apps/desktop/win/src/version.py
grep -r "0.0.0.2" apps/extension/chrome/manifest.json

# 7. Check README was created
ls -la versions/0.0.0.2/
cat versions/0.0.0.2/readme.md
```

---

## 📦 Step 9: Dependencies (5 min)

Create `scripts/requirements.txt`:

```txt
requests>=2.31.0
python-dateutil>=2.8.2
```

Install:
```bash
pip install -r scripts/requirements.txt
```

---

## 📚 Step 10: Documentation (30 min)

Create `scripts/README.md`:

```markdown
# Version Bump Script

Automated version management for ASPS project.

## Quick Start

```bash
# Install dependencies
pip install -r requirements.txt

# Set GitHub token
export GITHUB_TOKEN="your_github_token"

# Bump patch version (0.0.0.1 → 0.0.0.2)
python version_bump.py

# Preview changes
python version_bump.py --dry-run
```

## Usage

See [ASPS-243-version-bump-design.md](../.planning/ASPS-243-version-bump-design.md) for full documentation.

## Testing

```bash
# Run unit tests
python -m unittest tests/test_version_bump.py

# Integration test
python version_bump.py --dry-run
```
```

---

## ✅ Definition of Done

Before marking as "Ready for QA":

1. **Code Complete**
   - [ ] All functions implemented
   - [ ] Error handling in place
   - [ ] Dry-run mode works

2. **Testing**
   - [ ] Unit tests pass
   - [ ] Integration tests pass
   - [ ] Tested on Linux (primary environment)
   - [ ] (Optional) Tested on Windows/macOS

3. **Documentation**
   - [ ] README.md created
   - [ ] Code comments added
   - [ ] Usage examples provided

4. **JIRA**
   - [ ] Update ASPS-244 status to "Ready for QA"
   - [ ] Add comment: "Implementation complete. Tested dry-run mode successfully."
   - [ ] Add label: `ready-for-qa`

---

## 🚨 Common Issues & Solutions

### Issue: "ModuleNotFoundError: No module named 'requests'"
**Solution:**
```bash
pip install -r scripts/requirements.txt
```

### Issue: "Version element not found in XML"
**Solution:** Check that `.csproj` file has `<Version>` tag in `<PropertyGroup>`

### Issue: "GitHub API rate limit exceeded"
**Solution:** Use authenticated requests with GITHUB_TOKEN

### Issue: "Invalid version format"
**Solution:** Ensure version follows X.X.X.X format (4 components)

---

## 📞 Support

**Questions?** Ask Alex (CTO) 🧠 or post in #dev channel

---

## 🔗 Related Documents

- [ASPS-243 Technical Design](./ASPS-243-version-bump-design.md)
- [GitHub API Documentation](https://docs.github.com/en/rest)
- [Python unittest](https://docs.python.org/3/library/unittest.html)

---

**Good luck, Yuri! 🐍 You got this!**
