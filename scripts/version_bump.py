#!/usr/bin/env python3
"""
Version Bump & README Automation Script
ASPS-243: Automatically bump version across all project files and generate release notes.
"""

import argparse
import json
import os
import re
import sys
import subprocess
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, List, Optional, Tuple
import xml.etree.ElementTree as ET


class VersionBumper:
    """Handles version bumping across multiple file types."""
    
    def __init__(self, config_path: str = "version_config.json"):
        """Initialize with configuration file."""
        self.script_dir = Path(__file__).parent
        self.repo_root = self.script_dir.parent
        self.config_path = self.script_dir / config_path
        self.config = self._load_config()
        
    def _load_config(self) -> Dict:
        """Load configuration from JSON file."""
        if not self.config_path.exists():
            raise FileNotFoundError(f"Config file not found: {self.config_path}")
        
        with open(self.config_path, 'r', encoding='utf-8') as f:
            return json.load(f)
    
    def read_current_version(self) -> str:
        """Read current version from the first version file."""
        first_file = self.config['version_files'][0]
        file_path = self.repo_root / first_file['path']
        
        if first_file['type'] == 'xml':
            return self._read_xml_version(file_path, first_file['xpath'])
        elif first_file['type'] == 'python':
            return self._read_python_version(file_path)
        elif first_file['type'] == 'json':
            return self._read_json_version(file_path, first_file['key'])
        else:
            raise ValueError(f"Unsupported file type: {first_file['type']}")
    
    def _read_xml_version(self, file_path: Path, xpath: str) -> str:
        """Read version from XML file."""
        tree = ET.parse(file_path)
        root = tree.getroot()
        
        # Simple xpath implementation for .//PropertyGroup/Version
        for prop_group in root.findall('.//PropertyGroup'):
            version_elem = prop_group.find('Version')
            if version_elem is not None and version_elem.text:
                return version_elem.text.strip()
        
        raise ValueError(f"Version not found in {file_path}")
    
    def _read_python_version(self, file_path: Path) -> str:
        """Read version from Python file."""
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        match = re.search(r'VERSION\s*=\s*["\']([^"\']+)["\']', content)
        if not match:
            raise ValueError(f"VERSION not found in {file_path}")
        
        return match.group(1)
    
    def _read_json_version(self, file_path: Path, key: str) -> str:
        """Read version from JSON file."""
        with open(file_path, 'r', encoding='utf-8') as f:
            data = json.load(f)
        
        if key not in data:
            raise ValueError(f"Key '{key}' not found in {file_path}")
        
        return data[key]
    
    @staticmethod
    def increment_version(version: str) -> str:
        """Increment version number (last digit)."""
        parts = version.split('.')
        if len(parts) != 4:
            raise ValueError(f"Version must be in format X.X.X.X, got: {version}")
        
        parts[-1] = str(int(parts[-1]) + 1)
        return '.'.join(parts)
    
    def update_all_files(self, new_version: str, dry_run: bool = False) -> List[str]:
        """Update version in all configured files."""
        updated_files = []
        
        for file_config in self.config['version_files']:
            file_path = self.repo_root / file_config['path']
            
            if not file_path.exists():
                print(f"⚠️  Warning: File not found: {file_path}")
                continue
            
            if dry_run:
                print(f"[DRY RUN] Would update: {file_path}")
            else:
                if file_config['type'] == 'xml':
                    self._update_xml_version(file_path, file_config['xpath'], new_version)
                elif file_config['type'] == 'python':
                    self._update_python_version(file_path, new_version)
                elif file_config['type'] == 'json':
                    self._update_json_version(file_path, file_config['key'], new_version)
                
                print(f"✅ Updated: {file_config['path']}")
            
            updated_files.append(file_config['path'])
        
        return updated_files
    
    def _update_xml_version(self, file_path: Path, xpath: str, new_version: str):
        """Update version in XML file."""
        tree = ET.parse(file_path)
        root = tree.getroot()
        
        for prop_group in root.findall('.//PropertyGroup'):
            version_elem = prop_group.find('Version')
            if version_elem is not None:
                version_elem.text = new_version
                break
        
        tree.write(file_path, encoding='utf-8', xml_declaration=True)
    
    def _update_python_version(self, file_path: Path, new_version: str):
        """Update version in Python file."""
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        content = re.sub(
            r'VERSION\s*=\s*["\'][^"\']+["\']',
            f'VERSION = "{new_version}"',
            content
        )
        
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(content)
    
    def _update_json_version(self, file_path: Path, key: str, new_version: str):
        """Update version in JSON file."""
        with open(file_path, 'r', encoding='utf-8') as f:
            data = json.load(f)
        
        data[key] = new_version
        
        with open(file_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
            f.write('\n')  # Add trailing newline
    
    def get_commits_since_tag(self, tag: Optional[str] = None) -> List[str]:
        """Get commit messages since last tag or all commits."""
        try:
            if tag:
                cmd = ['git', 'log', f'{tag}..HEAD', '--oneline', '--no-merges']
            else:
                # Get last 10 commits if no tag exists
                cmd = ['git', 'log', '-10', '--oneline', '--no-merges']
            
            result = subprocess.run(
                cmd,
                cwd=self.repo_root,
                capture_output=True,
                text=True,
                check=True
            )
            
            commits = []
            for line in result.stdout.strip().split('\n'):
                if line:
                    # Remove commit hash, keep message
                    commits.append(line.split(' ', 1)[1] if ' ' in line else line)
            
            return commits
        except subprocess.CalledProcessError:
            return []
    
    def get_last_version_tag(self) -> Optional[str]:
        """Get the last version tag from git."""
        try:
            result = subprocess.run(
                ['git', 'tag', '-l', f"{self.config.get('tag_prefix', 'v')}*", '--sort=-version:refname'],
                cwd=self.repo_root,
                capture_output=True,
                text=True,
                check=True
            )
            
            tags = result.stdout.strip().split('\n')
            return tags[0] if tags and tags[0] else None
        except subprocess.CalledProcessError:
            return None
    
    def generate_readme(self, version: str, commits: List[str]) -> str:
        """Generate README content for version."""
        now = datetime.now(timezone.utc)
        
        readme = f"""# Version {version}
**Date:** {now.strftime('%Y-%m-%d %H:%M')} UTC
**Deployed by:** [auto]

## Changes
"""
        
        if commits:
            for commit in commits:
                readme += f"- {commit}\n"
        else:
            readme += "- No commits found since last release\n"
        
        return readme
    
    def create_version_directory(self, version: str, readme_content: str, dry_run: bool = False) -> Path:
        """Create version directory with README."""
        versions_dir = self.repo_root / self.config['versions_dir']
        version_dir = versions_dir / version
        
        if dry_run:
            print(f"[DRY RUN] Would create: {version_dir}/readme.md")
            return version_dir
        
        version_dir.mkdir(parents=True, exist_ok=True)
        
        readme_path = version_dir / 'readme.md'
        with open(readme_path, 'w', encoding='utf-8') as f:
            f.write(readme_content)
        
        print(f"✅ Created: {readme_path}")
        return version_dir
    
    def git_commit_and_tag(self, version: str, updated_files: List[str], dry_run: bool = False):
        """Commit changes and create git tag."""
        if dry_run:
            print(f"[DRY RUN] Would commit and tag: {version}")
            return
        
        if not self.config.get('git_auto_commit', False):
            print("⚠️  Git auto-commit disabled in config")
            return
        
        try:
            # Add updated files
            files_to_add = updated_files + [f"{self.config['versions_dir']}/{version}/readme.md"]
            for file_path in files_to_add:
                subprocess.run(
                    ['git', 'add', file_path],
                    cwd=self.repo_root,
                    check=True
                )
            
            # Commit
            commit_msg = self.config.get('commit_message_template', 'Version bump: {version}').format(version=version)
            subprocess.run(
                ['git', 'commit', '-m', commit_msg],
                cwd=self.repo_root,
                check=True
            )
            print(f"✅ Committed: {commit_msg}")
            
            # Tag
            if self.config.get('git_auto_tag', False):
                tag_name = f"{self.config.get('tag_prefix', 'v')}{version}"
                tag_msg = self.config.get('tag_message_template', 'Release {version}').format(version=version)
                subprocess.run(
                    ['git', 'tag', '-a', tag_name, '-m', tag_msg],
                    cwd=self.repo_root,
                    check=True
                )
                print(f"✅ Tagged: {tag_name}")
            
        except subprocess.CalledProcessError as e:
            print(f"❌ Git operation failed: {e}")
            sys.exit(1)
    
    def run(self, dry_run: bool = False) -> Tuple[str, List[str]]:
        """Run the version bump process."""
        # Read current version
        current_version = self.read_current_version()
        print(f"📌 Current version: {current_version}")
        
        # Increment version
        new_version = self.increment_version(current_version)
        print(f"🚀 New version: {new_version}")
        
        # Update all files
        print("\n📝 Updating version files...")
        updated_files = self.update_all_files(new_version, dry_run)
        
        # Get commits since last tag
        print("\n📜 Collecting commits...")
        last_tag = self.get_last_version_tag()
        commits = self.get_commits_since_tag(last_tag)
        print(f"Found {len(commits)} commits since {last_tag or 'beginning'}")
        
        # Generate README
        print("\n📄 Generating README...")
        readme_content = self.generate_readme(new_version, commits)
        version_dir = self.create_version_directory(new_version, readme_content, dry_run)
        
        # Git commit and tag
        if not dry_run:
            print("\n🔖 Committing and tagging...")
            self.git_commit_and_tag(new_version, updated_files)
        
        return new_version, updated_files


def main():
    """Main entry point."""
    parser = argparse.ArgumentParser(description='Version Bump & README Automation')
    parser.add_argument(
        '--dry-run',
        action='store_true',
        help='Preview changes without modifying files'
    )
    parser.add_argument(
        '--config',
        default='version_config.json',
        help='Path to config file (default: version_config.json)'
    )
    
    args = parser.parse_args()
    
    try:
        bumper = VersionBumper(config_path=args.config)
        new_version, updated_files = bumper.run(dry_run=args.dry_run)
        
        print("\n" + "="*60)
        if args.dry_run:
            print("🔍 DRY RUN COMPLETE - No files were modified")
        else:
            print("✅ VERSION BUMP COMPLETE!")
            print(f"📦 New version: {new_version}")
            print(f"📁 Files updated: {len(updated_files)}")
            print(f"📋 README: versions/{new_version}/readme.md")
        print("="*60)
        
    except Exception as e:
        print(f"\n❌ Error: {e}", file=sys.stderr)
        sys.exit(1)


if __name__ == '__main__':
    main()
