"""
Unit Tests for CacheManager
Tests local caching with TTL support
"""

import unittest
from unittest.mock import Mock, patch, MagicMock, mock_open
import sys
import os
import time
import json
import tempfile
from pathlib import Path

# Add parent directory to path for imports
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from cache_manager import CacheManager, CacheEntry


class TestCacheEntry(unittest.TestCase):
    """Test suite for CacheEntry dataclass"""
    
    #region Constructor Tests
    
    def test_constructor_creates_entry_with_all_fields(self):
        """Constructor should create entry with all required fields"""
        entry = CacheEntry(
            url="example.com",
            score=75,
            risk_type=[1, 2],
            protective_action=1,
            ttl=3600,
            saved_at=time.time()
        )
        
        self.assertEqual(entry.url, "example.com")
        self.assertEqual(entry.score, 75)
        self.assertEqual(entry.risk_type, [1, 2])
        self.assertEqual(entry.protective_action, 1)
        self.assertEqual(entry.ttl, 3600)
        self.assertIsInstance(entry.saved_at, float)
    
    #endregion
    
    #region is_expired Tests
    
    def test_is_expired_returns_false_for_fresh_entry(self):
        """Should return False for entry within TTL"""
        entry = CacheEntry(
            url="example.com",
            score=50,
            risk_type=[],
            protective_action=0,
            ttl=3600,
            saved_at=time.time()
        )
        
        self.assertFalse(entry.is_expired())
    
    def test_is_expired_returns_true_for_old_entry(self):
        """Should return True for entry beyond TTL"""
        entry = CacheEntry(
            url="example.com",
            score=50,
            risk_type=[],
            protective_action=0,
            ttl=10,
            saved_at=time.time() - 20  # 20 seconds ago, TTL is 10
        )
        
        self.assertTrue(entry.is_expired())
    
    def test_is_expired_edge_case_exact_ttl(self):
        """Should return True when exactly at TTL boundary"""
        now = time.time()
        entry = CacheEntry(
            url="example.com",
            score=50,
            risk_type=[],
            protective_action=0,
            ttl=10,
            saved_at=now - 10.1  # Just over TTL
        )
        
        self.assertTrue(entry.is_expired())
    
    #endregion
    
    #region Serialization Tests
    
    def test_to_dict_returns_all_fields(self):
        """to_dict should return all fields as dictionary"""
        entry = CacheEntry(
            url="test.com",
            score=80,
            risk_type=[1],
            protective_action=2,
            ttl=7200,
            saved_at=123456.789
        )
        
        result = entry.to_dict()
        
        self.assertIsInstance(result, dict)
        self.assertEqual(result['url'], "test.com")
        self.assertEqual(result['score'], 80)
        self.assertEqual(result['risk_type'], [1])
        self.assertEqual(result['protective_action'], 2)
        self.assertEqual(result['ttl'], 7200)
        self.assertEqual(result['saved_at'], 123456.789)
    
    def test_from_dict_creates_entry_from_dictionary(self):
        """from_dict should create CacheEntry from dictionary"""
        data = {
            'url': 'test.com',
            'score': 90,
            'risk_type': [2, 3],
            'protective_action': 1,
            'ttl': 1800,
            'saved_at': 987654.321
        }
        
        entry = CacheEntry.from_dict(data)
        
        self.assertEqual(entry.url, "test.com")
        self.assertEqual(entry.score, 90)
        self.assertEqual(entry.risk_type, [2, 3])
        self.assertEqual(entry.protective_action, 1)
        self.assertEqual(entry.ttl, 1800)
        self.assertEqual(entry.saved_at, 987654.321)
    
    #endregion


class TestCacheManager(unittest.TestCase):
    """Test suite for CacheManager class"""
    
    def setUp(self):
        """Set up test fixtures"""
        # Use temporary directory for cache
        self.temp_dir = tempfile.mkdtemp()
        
        # Patch DATA_DIR and CACHE_FILE
        self.data_dir_patcher = patch('cache_manager.DATA_DIR', self.temp_dir)
        self.cache_file_patcher = patch('cache_manager.CACHE_FILE', 'test_cache.json')
        
        self.data_dir_patcher.start()
        self.cache_file_patcher.start()
        
        self.manager = CacheManager()
    
    def tearDown(self):
        """Clean up after tests"""
        self.data_dir_patcher.stop()
        self.cache_file_patcher.stop()
        
        # Clean up temp directory
        import shutil
        if os.path.exists(self.temp_dir):
            shutil.rmtree(self.temp_dir)
    
    #region Constructor Tests
    
    def test_constructor_initializes_cache_dict(self):
        """Constructor should initialize empty cache dict"""
        self.assertIsInstance(self.manager._cache, dict)
    
    def test_constructor_creates_data_directory(self):
        """Constructor should create data directory if needed"""
        self.assertTrue(self.manager.data_dir.exists())
    
    #endregion
    
    #region normalize_url Tests
    
    def test_normalize_url_extracts_domain(self):
        """Should extract domain from full URL"""
        result = self.manager._normalize_url("https://example.com/path?query=1")
        self.assertEqual(result, "example.com")
    
    def test_normalize_url_lowercases_domain(self):
        """Should convert domain to lowercase"""
        result = self.manager._normalize_url("https://EXAMPLE.COM/Path")
        self.assertEqual(result, "example.com")
    
    def test_normalize_url_handles_subdomain(self):
        """Should preserve subdomain in normalization"""
        result = self.manager._normalize_url("https://www.example.com/page")
        self.assertEqual(result, "www.example.com")
    
    def test_normalize_url_handles_port(self):
        """Should include port in normalized URL"""
        result = self.manager._normalize_url("https://example.com:8080/page")
        self.assertEqual(result, "example.com:8080")
    
    def test_normalize_url_handles_invalid_url(self):
        """Should handle invalid URL gracefully"""
        result = self.manager._normalize_url("not-a-url")
        # URL without scheme results in empty netloc, falls back to lowercase
        self.assertIsInstance(result, str)
    
    #endregion
    
    #region get Tests
    
    def test_get_returns_none_for_missing_entry(self):
        """Should return None when URL not in cache"""
        result = self.manager.get("https://example.com")
        self.assertIsNone(result)
    
    def test_get_returns_cached_entry(self):
        """Should return cached entry when present"""
        self.manager.set("https://example.com", 80, [1], 0, 3600)
        
        result = self.manager.get("https://example.com")
        
        self.assertIsNotNone(result)
        self.assertEqual(result.score, 80)
        self.assertEqual(result.risk_type, [1])
    
    def test_get_returns_none_for_expired_entry(self):
        """Should return None and remove expired entry"""
        # Manually add expired entry
        key = self.manager._normalize_url("https://example.com")
        expired_entry = CacheEntry(
            url=key,
            score=50,
            risk_type=[],
            protective_action=0,
            ttl=10,
            saved_at=time.time() - 20  # Expired
        )
        self.manager._cache[key] = expired_entry
        
        result = self.manager.get("https://example.com")
        
        self.assertIsNone(result)
        self.assertNotIn(key, self.manager._cache)
    
    #endregion
    
    #region set Tests
    
    def test_set_adds_entry_to_cache(self):
        """Should add new entry to cache"""
        self.manager.set("https://test.com", 75, [1, 2], 1, 7200)
        
        result = self.manager.get("https://test.com")
        
        self.assertIsNotNone(result)
        self.assertEqual(result.score, 75)
        self.assertEqual(result.risk_type, [1, 2])
        self.assertEqual(result.protective_action, 1)
        self.assertEqual(result.ttl, 7200)
    
    def test_set_overwrites_existing_entry(self):
        """Should overwrite existing cache entry"""
        self.manager.set("https://test.com", 50, [], 0, 3600)
        self.manager.set("https://test.com", 90, [3], 2, 1800)
        
        result = self.manager.get("https://test.com")
        
        self.assertEqual(result.score, 90)
        self.assertEqual(result.risk_type, [3])
    
    #endregion
    
    #region has Tests
    
    def test_has_returns_true_for_cached_url(self):
        """Should return True when URL is cached"""
        self.manager.set("https://example.com", 60, [], 0, 3600)
        
        self.assertTrue(self.manager.has("https://example.com"))
    
    def test_has_returns_false_for_missing_url(self):
        """Should return False when URL not cached"""
        self.assertFalse(self.manager.has("https://notcached.com"))
    
    def test_has_returns_false_for_expired_url(self):
        """Should return False for expired cache entry"""
        key = self.manager._normalize_url("https://example.com")
        expired_entry = CacheEntry(
            url=key,
            score=50,
            risk_type=[],
            protective_action=0,
            ttl=10,
            saved_at=time.time() - 20
        )
        self.manager._cache[key] = expired_entry
        
        self.assertFalse(self.manager.has("https://example.com"))
    
    #endregion
    
    #region remove Tests
    
    def test_remove_deletes_entry_from_cache(self):
        """Should remove entry from cache"""
        self.manager.set("https://example.com", 50, [], 0, 3600)
        
        self.manager.remove("https://example.com")
        
        self.assertFalse(self.manager.has("https://example.com"))
    
    def test_remove_handles_missing_entry(self):
        """Should not raise error when removing non-existent entry"""
        # Should not raise exception
        try:
            self.manager.remove("https://notcached.com")
        except Exception as e:
            self.fail(f"Should not raise exception: {e}")
    
    #endregion
    
    #region clear Tests
    
    def test_clear_removes_all_entries(self):
        """Should remove all cache entries"""
        self.manager.set("https://example1.com", 50, [], 0, 3600)
        self.manager.set("https://example2.com", 60, [], 0, 3600)
        self.manager.set("https://example3.com", 70, [], 0, 3600)
        
        self.manager.clear()
        
        self.assertEqual(len(self.manager._cache), 0)
        self.assertFalse(self.manager.has("https://example1.com"))
    
    #endregion
    
    #region get_stats Tests
    
    def test_get_stats_returns_cache_info(self):
        """Should return cache statistics"""
        self.manager.set("https://test1.com", 50, [], 0, 3600)
        self.manager.set("https://test2.com", 60, [], 0, 3600)
        
        stats = self.manager.get_stats()
        
        self.assertIsInstance(stats, dict)
        self.assertIn('total_entries', stats)
        self.assertIn('cache_file', stats)
        self.assertEqual(stats['total_entries'], 2)
    
    def test_get_stats_excludes_expired_entries(self):
        """Should not count expired entries in stats"""
        # Add fresh entry
        self.manager.set("https://fresh.com", 50, [], 0, 3600)
        
        # Add expired entry manually
        key = self.manager._normalize_url("https://expired.com")
        expired_entry = CacheEntry(
            url=key,
            score=50,
            risk_type=[],
            protective_action=0,
            ttl=10,
            saved_at=time.time() - 20
        )
        self.manager._cache[key] = expired_entry
        
        stats = self.manager.get_stats()
        
        self.assertEqual(stats['total_entries'], 1)  # Only fresh entry
    
    #endregion
    
    #region Persistence Tests
    
    def test_cache_persists_across_instances(self):
        """Cache should persist when creating new instance"""
        self.manager.set("https://persist.com", 85, [1], 1, 3600)
        
        # Create new instance (should load from file)
        new_manager = CacheManager()
        
        result = new_manager.get("https://persist.com")
        self.assertIsNotNone(result)
        self.assertEqual(result.score, 85)
    
    #endregion


if __name__ == '__main__':
    # Run tests with verbose output
    unittest.main(verbosity=2)
