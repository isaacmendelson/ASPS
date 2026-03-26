"""
Unit Tests for EventLogger
Tests JSON lines event logging
"""

import unittest
from unittest.mock import Mock, patch, MagicMock, mock_open
import sys
import os
import json
import tempfile
from pathlib import Path
from datetime import datetime, timedelta

# Add parent directory to path for imports
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from event_logger import EventLogger


class TestEventLogger(unittest.TestCase):
    """Test suite for EventLogger class"""
    
    def setUp(self):
        """Set up test fixtures"""
        # Use temporary directory for logs
        self.temp_dir = tempfile.mkdtemp()
        
        # Patch DATA_DIR and LOG_FILE
        self.data_dir_patcher = patch('event_logger.DATA_DIR', self.temp_dir)
        self.log_file_patcher = patch('event_logger.LOG_FILE', 'test_events.jsonl')
        
        self.data_dir_patcher.start()
        self.log_file_patcher.start()
        
        self.logger = EventLogger()
    
    def tearDown(self):
        """Clean up after tests"""
        self.data_dir_patcher.stop()
        self.log_file_patcher.stop()
        
        # Clean up temp directory
        import shutil
        if os.path.exists(self.temp_dir):
            shutil.rmtree(self.temp_dir)
    
    #region Constructor Tests
    
    def test_constructor_creates_data_directory(self):
        """Constructor should create data directory"""
        self.assertTrue(self.logger.data_dir.exists())
    
    def test_constructor_sets_log_file_path(self):
        """Constructor should set log file path"""
        self.assertIsInstance(self.logger.log_file, Path)
        self.assertTrue(str(self.logger.log_file).endswith('test_events.jsonl'))
    
    #endregion
    
    #region get_timestamp Tests
    
    def test_get_timestamp_returns_formatted_string(self):
        """Should return timestamp in correct format"""
        timestamp = self.logger._get_timestamp()
        
        self.assertIsInstance(timestamp, str)
        # Should match format: "YYYY-MM-DD HH:MM:SS"
        try:
            datetime.strptime(timestamp, "%Y-%m-%d %H:%M:%S")
        except ValueError:
            self.fail("Timestamp format is incorrect")
    
    #endregion
    
    #region log Tests
    
    def test_log_creates_json_entry(self):
        """Should create valid JSON log entry"""
        self.logger.log("TestEvent", {"key": "value"}, "sent")
        
        # Read the log file
        with open(self.logger.log_file, 'r') as f:
            line = f.readline()
        
        entry = json.loads(line)
        
        self.assertEqual(entry['type'], "TestEvent")
        self.assertEqual(entry['direction'], "sent")
        self.assertEqual(entry['data']['key'], "value")
        self.assertIn('timestamp', entry)
    
    def test_log_appends_to_file(self):
        """Should append entries without overwriting"""
        self.logger.log("Event1", {"num": 1})
        self.logger.log("Event2", {"num": 2})
        self.logger.log("Event3", {"num": 3})
        
        with open(self.logger.log_file, 'r') as f:
            lines = f.readlines()
        
        self.assertEqual(len(lines), 3)
        
        entry1 = json.loads(lines[0])
        entry2 = json.loads(lines[1])
        entry3 = json.loads(lines[2])
        
        self.assertEqual(entry1['type'], "Event1")
        self.assertEqual(entry2['type'], "Event2")
        self.assertEqual(entry3['type'], "Event3")
    
    def test_log_handles_unicode_data(self):
        """Should handle Unicode characters in data"""
        self.logger.log("UnicodeTest", {"message": "שלום עולם 🎉"})
        
        with open(self.logger.log_file, 'r', encoding='utf-8') as f:
            line = f.readline()
        
        entry = json.loads(line)
        self.assertEqual(entry['data']['message'], "שלום עולם 🎉")
    
    def test_log_handles_empty_direction(self):
        """Should handle empty direction string"""
        self.logger.log("Event", {"test": True}, "")
        
        with open(self.logger.log_file, 'r') as f:
            entry = json.loads(f.readline())
        
        self.assertEqual(entry['direction'], "")
    
    #endregion
    
    #region log_sent Tests
    
    def test_log_sent_adds_sent_direction(self):
        """Should add 'sent' direction to log entry"""
        self.logger.log_sent("SentEvent", {"key": "value"})
        
        with open(self.logger.log_file, 'r') as f:
            entry = json.loads(f.readline())
        
        self.assertEqual(entry['direction'], "sent")
        self.assertEqual(entry['type'], "SentEvent")
    
    #endregion
    
    #region log_received Tests
    
    def test_log_received_adds_received_direction(self):
        """Should add 'received' direction to log entry"""
        self.logger.log_received("ReceivedEvent", {"data": "test"})
        
        with open(self.logger.log_file, 'r') as f:
            entry = json.loads(f.readline())
        
        self.assertEqual(entry['direction'], "received")
        self.assertEqual(entry['type'], "ReceivedEvent")
    
    #endregion
    
    #region log_event Tests
    
    def test_log_event_adds_local_direction(self):
        """Should add 'local' direction to log entry"""
        self.logger.log_event("LocalEvent", {"local": True})
        
        with open(self.logger.log_file, 'r') as f:
            entry = json.loads(f.readline())
        
        self.assertEqual(entry['direction'], "local")
        self.assertEqual(entry['type'], "LocalEvent")
    
    #endregion
    
    #region log_error Tests
    
    def test_log_error_creates_error_entry(self):
        """Should create error log entry"""
        self.logger.log_error("Test error occurred")
        
        with open(self.logger.log_file, 'r') as f:
            entry = json.loads(f.readline())
        
        self.assertEqual(entry['type'], "Error")
        self.assertEqual(entry['data']['error'], "Test error occurred")
    
    def test_log_error_includes_context(self):
        """Should include context when provided"""
        context = {"function": "test_func", "line": 42}
        self.logger.log_error("Error with context", context)
        
        with open(self.logger.log_file, 'r') as f:
            entry = json.loads(f.readline())
        
        self.assertIn('context', entry['data'])
        self.assertEqual(entry['data']['context']['function'], "test_func")
    
    def test_log_error_without_context(self):
        """Should work without context parameter"""
        self.logger.log_error("Simple error")
        
        with open(self.logger.log_file, 'r') as f:
            entry = json.loads(f.readline())
        
        self.assertNotIn('context', entry['data'])
    
    #endregion
    
    #region get_recent_logs Tests
    
    def test_get_recent_logs_returns_all_when_less_than_count(self):
        """Should return all logs when total is less than count"""
        self.logger.log("Event1", {"num": 1})
        self.logger.log("Event2", {"num": 2})
        self.logger.log("Event3", {"num": 3})
        
        logs = self.logger.get_recent_logs(count=100)
        
        self.assertEqual(len(logs), 3)
    
    def test_get_recent_logs_returns_last_n_entries(self):
        """Should return only last N entries when more exist"""
        for i in range(10):
            self.logger.log(f"Event{i}", {"num": i})
        
        logs = self.logger.get_recent_logs(count=5)
        
        self.assertEqual(len(logs), 5)
        # Should be entries 5-9
        self.assertEqual(logs[0]['type'], "Event5")
        self.assertEqual(logs[-1]['type'], "Event9")
    
    def test_get_recent_logs_returns_empty_when_no_file(self):
        """Should return empty list when log file doesn't exist"""
        # Create new logger with non-existent file
        new_temp = tempfile.mkdtemp()
        with patch('event_logger.DATA_DIR', new_temp):
            with patch('event_logger.LOG_FILE', 'nonexistent.jsonl'):
                new_logger = EventLogger()
                logs = new_logger.get_recent_logs()
        
        import shutil
        shutil.rmtree(new_temp)
        
        self.assertEqual(logs, [])
    
    def test_get_recent_logs_skips_invalid_json(self):
        """Should skip lines with invalid JSON"""
        self.logger.log("ValidEvent", {"valid": True})
        
        # Append invalid JSON
        with open(self.logger.log_file, 'a') as f:
            f.write("INVALID JSON LINE\n")
        
        self.logger.log("AnotherValid", {"valid": True})
        
        logs = self.logger.get_recent_logs()
        
        # Should return only valid entries
        self.assertEqual(len(logs), 2)
        self.assertEqual(logs[0]['type'], "ValidEvent")
        self.assertEqual(logs[1]['type'], "AnotherValid")
    
    #endregion
    
    #region get_logs_by_type Tests
    
    def test_get_logs_by_type_filters_correctly(self):
        """Should return only logs matching type"""
        self.logger.log("TypeA", {"data": 1})
        self.logger.log("TypeB", {"data": 2})
        self.logger.log("TypeA", {"data": 3})
        self.logger.log("TypeC", {"data": 4})
        self.logger.log("TypeA", {"data": 5})
        
        logs = self.logger.get_logs_by_type("TypeA")
        
        self.assertEqual(len(logs), 3)
        for log in logs:
            self.assertEqual(log['type'], "TypeA")
    
    def test_get_logs_by_type_returns_empty_when_no_match(self):
        """Should return empty list when type not found"""
        self.logger.log("EventA", {})
        self.logger.log("EventB", {})
        
        logs = self.logger.get_logs_by_type("NonExistent")
        
        self.assertEqual(logs, [])
    
    def test_get_logs_by_type_respects_count_limit(self):
        """Should limit results to count parameter"""
        for i in range(10):
            self.logger.log("TestType", {"num": i})
        
        logs = self.logger.get_logs_by_type("TestType", count=5)
        
        self.assertEqual(len(logs), 5)
        # Should be last 5 (entries 5-9)
        self.assertEqual(logs[0]['data']['num'], 5)
        self.assertEqual(logs[-1]['data']['num'], 9)
    
    #endregion
    
    #region get_stats Tests
    
    def test_get_stats_returns_correct_count(self):
        """Should return correct entry count"""
        for i in range(5):
            self.logger.log("Event", {"num": i})
        
        stats = self.logger.get_stats()
        
        self.assertEqual(stats['total_entries'], 5)
    
    def test_get_stats_returns_file_size(self):
        """Should return file size"""
        self.logger.log("Event", {"data": "test"})
        
        stats = self.logger.get_stats()
        
        self.assertIn('file_size', stats)
        self.assertGreater(stats['file_size'], 0)
    
    def test_get_stats_returns_zero_when_no_file(self):
        """Should return zeros when log file doesn't exist"""
        new_temp = tempfile.mkdtemp()
        with patch('event_logger.DATA_DIR', new_temp):
            with patch('event_logger.LOG_FILE', 'nonexistent.jsonl'):
                new_logger = EventLogger()
                stats = new_logger.get_stats()
        
        import shutil
        shutil.rmtree(new_temp)
        
        self.assertEqual(stats['total_entries'], 0)
        self.assertEqual(stats['file_size'], 0)
    
    #endregion
    
    #region clear_old_logs Tests
    
    def test_clear_old_logs_removes_old_entries(self):
        """Should remove entries older than specified days"""
        # Add old entry (manually)
        old_timestamp = (datetime.now() - timedelta(days=40)).strftime("%Y-%m-%d %H:%M:%S")
        old_entry = {
            "timestamp": old_timestamp,
            "type": "OldEvent",
            "direction": "",
            "data": {}
        }
        with open(self.logger.log_file, 'a') as f:
            f.write(json.dumps(old_entry) + '\n')
        
        # Add recent entry
        self.logger.log("RecentEvent", {})
        
        # Clear logs older than 30 days
        self.logger.clear_old_logs(days=30)
        
        # Read remaining logs
        logs = self.logger.get_recent_logs()
        
        # Should only have recent entry
        self.assertEqual(len(logs), 1)
        self.assertEqual(logs[0]['type'], "RecentEvent")
    
    def test_clear_old_logs_keeps_recent_entries(self):
        """Should keep entries within the retention period"""
        self.logger.log("Recent1", {})
        self.logger.log("Recent2", {})
        self.logger.log("Recent3", {})
        
        self.logger.clear_old_logs(days=30)
        
        logs = self.logger.get_recent_logs()
        self.assertEqual(len(logs), 3)
    
    #endregion


if __name__ == '__main__':
    # Run tests with verbose output
    unittest.main(verbosity=2)
