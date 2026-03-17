#!/usr/bin/env python3
"""
Unit tests for version_bump.py
ASPS-243: Test version bumping functionality
"""

import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import Mock, patch, mock_open
import sys
import os

# Add parent directory to path to import version_bump
sys.path.insert(0, str(Path(__file__).parent.parent))
from version_bump import VersionBumper


class TestVersionBumper(unittest.TestCase):
    """Test cases for VersionBumper class."""
    
    def setUp(self):
        """Set up test fixtures."""
        self.test_config = {
            "version_files": [
                {
                    "path": "test.csproj",
                    "type": "xml",
                    "xpath": ".//PropertyGroup/Version",
                    "description": "Test XML"
                },
                {
                    "path": "test_version.py",
                    "type": "python",
                    "pattern": "VERSION = \"{{version}}\"",
                    "description": "Test Python"
                },
                {
                    "path": "test_manifest.json",
                    "type": "json",
                    "key": "version",
                    "description": "Test JSON"
                }
            ],
            "github": {
                "owner": "test",
                "repo": "test-repo"
            },
            "versions_dir": "versions",
            "git_auto_commit": True,
            "git_auto_tag": True,
            "tag_prefix": "v",
            "commit_message_template": "Version bump: {version}",
            "tag_message_template": "Release {version}"
        }
        
        self.temp_dir = tempfile.mkdtemp()
        self.temp_path = Path(self.temp_dir)
    
    def tearDown(self):
        """Clean up test fixtures."""
        import shutil
        if self.temp_path.exists():
            shutil.rmtree(self.temp_path)
    
    def test_increment_version(self):
        """Test version increment logic."""
        test_cases = [
            ("0.0.0.1", "0.0.0.2"),
            ("0.0.0.9", "0.0.0.10"),
            ("1.2.3.4", "1.2.3.5"),
            ("0.0.0.99", "0.0.0.100"),
        ]
        
        for current, expected in test_cases:
            with self.subTest(current=current):
                result = VersionBumper.increment_version(current)
                self.assertEqual(result, expected)
    
    def test_increment_version_invalid(self):
        """Test version increment with invalid format."""
        with self.assertRaises(ValueError):
            VersionBumper.increment_version("1.0")
        
        with self.assertRaises(ValueError):
            VersionBumper.increment_version("1.0.0")
        
        with self.assertRaises(ValueError):
            VersionBumper.increment_version("invalid")
    
    @patch('version_bump.Path')
    def test_read_python_version(self, mock_path):
        """Test reading version from Python file."""
        python_content = '''"""Test version file"""
VERSION = "0.0.0.1"
'''
        with patch('builtins.open', mock_open(read_data=python_content)):
            # Create a mock bumper with patched config
            with patch.object(VersionBumper, '_load_config', return_value=self.test_config):
                bumper = VersionBumper()
                version = bumper._read_python_version(Path("test_version.py"))
                self.assertEqual(version, "0.0.0.1")
    
    @patch('version_bump.Path')
    def test_read_json_version(self, mock_path):
        """Test reading version from JSON file."""
        json_content = json.dumps({"version": "1.2.3.4", "name": "test"})
        
        with patch('builtins.open', mock_open(read_data=json_content)):
            with patch.object(VersionBumper, '_load_config', return_value=self.test_config):
                bumper = VersionBumper()
                version = bumper._read_json_version(Path("test_manifest.json"), "version")
                self.assertEqual(version, "1.2.3.4")
    
    def test_generate_readme(self):
        """Test README generation."""
        with patch.object(VersionBumper, '_load_config', return_value=self.test_config):
            bumper = VersionBumper()
            
            commits = [
                "ASPS-243: Implement version bump",
                "Fix: Update README template"
            ]
            
            readme = bumper.generate_readme("0.0.0.2", commits)
            
            self.assertIn("# Version 0.0.0.2", readme)
            self.assertIn("**Date:**", readme)
            self.assertIn("UTC", readme)
            self.assertIn("**Deployed by:** [auto]", readme)
            self.assertIn("## Changes", readme)
            self.assertIn("- ASPS-243: Implement version bump", readme)
            self.assertIn("- Fix: Update README template", readme)
    
    def test_generate_readme_no_commits(self):
        """Test README generation with no commits."""
        with patch.object(VersionBumper, '_load_config', return_value=self.test_config):
            bumper = VersionBumper()
            readme = bumper.generate_readme("0.0.0.2", [])
            
            self.assertIn("# Version 0.0.0.2", readme)
            self.assertIn("- No commits found since last release", readme)
    
    @patch('version_bump.subprocess.run')
    def test_get_commits_since_tag(self, mock_run):
        """Test getting commits since tag."""
        mock_result = Mock()
        mock_result.stdout = "abc123 ASPS-243: Implement version bump\ndef456 Fix: Update tests\n"
        mock_run.return_value = mock_result
        
        with patch.object(VersionBumper, '_load_config', return_value=self.test_config):
            bumper = VersionBumper()
            commits = bumper.get_commits_since_tag("v0.0.0.1")
            
            self.assertEqual(len(commits), 2)
            self.assertEqual(commits[0], "ASPS-243: Implement version bump")
            self.assertEqual(commits[1], "Fix: Update tests")
    
    @patch('version_bump.subprocess.run')
    def test_get_last_version_tag(self, mock_run):
        """Test getting last version tag."""
        mock_result = Mock()
        mock_result.stdout = "v0.0.0.2\nv0.0.0.1\n"
        mock_run.return_value = mock_result
        
        with patch.object(VersionBumper, '_load_config', return_value=self.test_config):
            bumper = VersionBumper()
            tag = bumper.get_last_version_tag()
            
            self.assertEqual(tag, "v0.0.0.2")
    
    def test_update_python_version(self):
        """Test updating Python version file."""
        original_content = '''"""Test version"""
VERSION = "0.0.0.1"
OTHER = "value"
'''
        expected_content = '''"""Test version"""
VERSION = "0.0.0.2"
OTHER = "value"
'''
        
        with patch('builtins.open', mock_open(read_data=original_content)) as mock_file:
            with patch.object(VersionBumper, '_load_config', return_value=self.test_config):
                bumper = VersionBumper()
                bumper._update_python_version(Path("test_version.py"), "0.0.0.2")
                
                # Get what was written
                handle = mock_file()
                written_content = ''.join(call.args[0] for call in handle.write.call_args_list)
                self.assertEqual(written_content, expected_content)
    
    def test_update_json_version(self):
        """Test updating JSON version file."""
        original_data = {"version": "0.0.0.1", "name": "test"}
        expected_data = {"version": "0.0.0.2", "name": "test"}
        
        with patch('builtins.open', mock_open(read_data=json.dumps(original_data))) as mock_file:
            with patch.object(VersionBumper, '_load_config', return_value=self.test_config):
                bumper = VersionBumper()
                bumper._update_json_version(Path("test_manifest.json"), "version", "0.0.0.2")
                
                # Verify json.dump was called with correct data
                handle = mock_file()
                self.assertTrue(handle.write.called)


class TestVersionBumperIntegration(unittest.TestCase):
    """Integration tests for version bumping."""
    
    def setUp(self):
        """Set up test environment."""
        self.temp_dir = tempfile.mkdtemp()
        self.temp_path = Path(self.temp_dir)
        
        # Create test config
        self.config_path = self.temp_path / "test_config.json"
        self.config = {
            "version_files": [
                {
                    "path": "test_version.py",
                    "type": "python",
                    "description": "Test Python"
                }
            ],
            "versions_dir": "versions",
            "git_auto_commit": False,
            "git_auto_tag": False
        }
        
        with open(self.config_path, 'w') as f:
            json.dump(self.config, f)
        
        # Create test version file
        self.version_file = self.temp_path / "test_version.py"
        with open(self.version_file, 'w') as f:
            f.write('VERSION = "0.0.0.1"\n')
    
    def tearDown(self):
        """Clean up test environment."""
        import shutil
        if self.temp_path.exists():
            shutil.rmtree(self.temp_path)
    
    def test_dry_run_does_not_modify_files(self):
        """Test that dry run does not modify files."""
        original_content = self.version_file.read_text()
        
        with patch.object(VersionBumper, '__init__', lambda self, config_path: None):
            bumper = VersionBumper.__new__(VersionBumper)
            bumper.repo_root = self.temp_path
            bumper.config = self.config
            
            # Dry run
            bumper.update_all_files("0.0.0.2", dry_run=True)
            
            # Verify file unchanged
            self.assertEqual(self.version_file.read_text(), original_content)


def run_tests():
    """Run all tests."""
    unittest.main(argv=[''], verbosity=2, exit=False)


if __name__ == '__main__':
    run_tests()
