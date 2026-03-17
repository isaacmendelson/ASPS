# Version Bump Automation

## Overview
Automated version bumping script that updates version numbers across multiple project files and generates release notes.

**Task:** ASPS-243

## Features
- ✅ Automatic version increment (X.X.X.X format)
- ✅ Updates 4 file types: XML, Python, JSON
- ✅ Generates release notes from git commits
- ✅ Creates versioned directories with README
- ✅ Git commit and tag automation
- ✅ Dry-run mode for preview

## Files Updated
1. `ASPSBackend14_J/ASPSBackend/ASPSBackend.csproj` - Backend version
2. `ASPSBackend14_J/WebApi/WebApi.csproj` - WebAPI version
3. `apps/desktop/win/src/version.py` - Desktop app version
4. `apps/extension/chrome/manifest.json` - Chrome extension version

## Usage

### Quick Start
```bash
# Run version bump (increments last digit)
python scripts/version_bump.py

# Preview changes without modifying files
python scripts/version_bump.py --dry-run
```

### Configuration
Edit `scripts/version_config.json` to customize:
- File paths and patterns
- GitHub repository settings
- Git commit/tag behavior
- Versions directory location

### Output
- Updates all configured version files
- Creates `versions/X.X.X.X/readme.md` with release notes
- Commits changes and creates git tag (if enabled)

## Requirements
```bash
pip install -r scripts/requirements.txt
```

## Testing
```bash
# Run unit tests
python scripts/tests/test_version_bump.py

# All tests should pass before deployment
```

## Example Output
```
📌 Current version: 0.0.0.1
🚀 New version: 0.0.0.2

📝 Updating version files...
✅ Updated: ASPSBackend14_J/ASPSBackend/ASPSBackend.csproj
✅ Updated: ASPSBackend14_J/WebApi/WebApi.csproj
✅ Updated: apps/desktop/win/src/version.py
✅ Updated: apps/extension/chrome/manifest.json

📜 Collecting commits...
Found 10 commits since beginning

📄 Generating README...
✅ Created: versions/0.0.0.2/readme.md

🔖 Committing and tagging...
✅ Committed: Version bump: 0.0.0.2
✅ Tagged: v0.0.0.2
```

## Version Format
Versions follow the format: `X.X.X.X` (4 digits)
- Example: `0.0.0.1` → `0.0.0.2`
- The script increments the last digit only

## Git Integration
When `git_auto_commit` is enabled in config:
1. Stages all updated version files
2. Commits with message template
3. Creates annotated tag (if `git_auto_tag` enabled)

**Note:** Push manually or set up CI/CD automation.

## Troubleshooting

### Config File Not Found
```bash
cp scripts/version_config.json.example scripts/version_config.json
```

### Tests Failing
Ensure Python 3.8+ is installed and all dependencies are available.

### Git Commit Issues
Check git user configuration:
```bash
git config user.name "Your Name"
git config user.email "you@example.com"
```

## Architecture

### Functions
- `read_current_version()` - Read current version from first file
- `increment_version(v)` - Increment version number
- `update_all_files(new_version)` - Update all configured files
- `get_commits_since_tag(tag)` - Fetch git commits for changelog
- `generate_readme(version, commits)` - Create release notes
- `create_version_directory(version)` - Make versions/X.X.X.X/

### File Type Handlers
- **XML**: Uses ElementTree for .csproj files
- **Python**: Regex pattern matching for VERSION variable
- **JSON**: Standard json module for manifest.json

## Future Enhancements
- [ ] GitHub API integration for release creation
- [ ] Semantic versioning support (major.minor.patch)
- [ ] Custom increment targets (bump major/minor/patch)
- [ ] Rollback functionality
- [ ] Pre/post bump hooks

---

**Developer:** Yuri 🐍  
**Date:** 2026-03-17
