"""
Cache management for analysis results
"""

import json
import os
from datetime import datetime, timedelta
from pathlib import Path
from typing import Optional, Dict
from .logger import setup_logger


class CacheManager:
    """Manages caching of analysis results"""
    
    def __init__(self, cache_file: str = 'cache/results_cache.json', ttl_hours: int = 24):
        """
        Initialize cache manager
        
        Args:
            cache_file: Path to cache file
            ttl_hours: Time to live in hours
        """
        self.logger = setup_logger('cache_manager')
        self.cache_file = Path(cache_file)
        self.ttl_hours = ttl_hours
        
        # Create cache directory if doesn't exist
        self.cache_file.parent.mkdir(parents=True, exist_ok=True)
        
        # Initialize cache file if doesn't exist
        if not self.cache_file.exists():
            self._save_cache({})
    
    def get(self, url: str) -> Optional[Dict]:
        """
        Get cached result for URL
        
        Args:
            url: URL to lookup
        
        Returns:
            Cached result or None if not found/expired
        """
        cache = self._load_cache()
        
        if url not in cache:
            self.logger.debug(f"Cache miss for {url}")
            return None
        
        entry = cache[url]
        cached_time = datetime.fromisoformat(entry['cached_at'])
        
        # Check if expired
        if datetime.now() - cached_time > timedelta(hours=self.ttl_hours):
            self.logger.debug(f"Cache expired for {url}")
            del cache[url]
            self._save_cache(cache)
            return None
        
        self.logger.info(f"Cache hit for {url}")
        return entry['result']
    
    def set(self, url: str, result: Dict) -> None:
        """
        Cache result for URL
        
        Args:
            url: URL key
            result: Analysis result to cache
        """
        cache = self._load_cache()
        
        cache[url] = {
            'result': result,
            'cached_at': datetime.now().isoformat()
        }
        
        self._save_cache(cache)
        self.logger.info(f"Cached result for {url}")
    
    def clear(self) -> None:
        """Clear all cache"""
        self._save_cache({})
        self.logger.info("Cache cleared")
    
    def cleanup_expired(self) -> int:
        """
        Remove expired entries
        
        Returns:
            Number of entries removed
        """
        cache = self._load_cache()
        original_count = len(cache)
        
        now = datetime.now()
        expired_keys = []
        
        for url, entry in cache.items():
            cached_time = datetime.fromisoformat(entry['cached_at'])
            if now - cached_time > timedelta(hours=self.ttl_hours):
                expired_keys.append(url)
        
        for key in expired_keys:
            del cache[key]
        
        self._save_cache(cache)
        
        removed = original_count - len(cache)
        if removed > 0:
            self.logger.info(f"Removed {removed} expired cache entries")
        
        return removed
    
    def _load_cache(self) -> Dict:
        """Load cache from file"""
        try:
            with open(self.cache_file, 'r') as f:
                return json.load(f)
        except (json.JSONDecodeError, FileNotFoundError):
            return {}
    
    def _save_cache(self, cache: Dict) -> None:
        """Save cache to file"""
        with open(self.cache_file, 'w') as f:
            json.dump(cache, f, indent=2)
