"""
AntiScam Desktop App - Cache Manager
Local caching with TTL support
"""

import os
import json
import time
from typing import Optional, Dict, Any
from dataclasses import dataclass, asdict
import logging
from pathlib import Path

from config import DATA_DIR, CACHE_FILE

logger = logging.getLogger(__name__)


@dataclass
class CacheEntry:
    """A cached URL check result"""
    url: str
    score: int
    risk_type: list
    protective_action: int
    ttl: int  # seconds
    saved_at: float  # timestamp
    
    def is_expired(self) -> bool:
        """Check if this cache entry has expired"""
        return time.time() - self.saved_at > self.ttl
    
    def to_dict(self) -> dict:
        return asdict(self)
    
    @classmethod
    def from_dict(cls, data: dict) -> 'CacheEntry':
        return cls(
            url=data['url'],
            score=data['score'],
            risk_type=data['risk_type'],
            protective_action=data['protective_action'],
            ttl=data['ttl'],
            saved_at=data['saved_at']
        )


class CacheManager:
    """Manages local cache for URL checks"""
    
    def __init__(self):
        self.data_dir = Path(os.path.expanduser(DATA_DIR))
        self.cache_file = self.data_dir / CACHE_FILE
        self._cache: Dict[str, CacheEntry] = {}
        self._load_cache()
        
    def _ensure_data_dir(self):
        """Create data directory if it doesn't exist"""
        self.data_dir.mkdir(parents=True, exist_ok=True)
        
    def _load_cache(self):
        """Load cache from disk"""
        self._ensure_data_dir()
        
        if not self.cache_file.exists():
            self._cache = {}
            return
            
        try:
            with open(self.cache_file, 'r', encoding='utf-8') as f:
                data = json.load(f)
                
            for url, entry_data in data.items():
                entry = CacheEntry.from_dict(entry_data)
                # Only load non-expired entries
                if not entry.is_expired():
                    self._cache[url] = entry
                    
            logger.info(f"Loaded {len(self._cache)} cache entries")
            
        except Exception as e:
            logger.error(f"Error loading cache: {e}")
            self._cache = {}
    
    def _save_cache(self):
        """Save cache to disk"""
        self._ensure_data_dir()
        
        try:
            # Remove expired entries before saving
            self._cleanup_expired()
            
            data = {url: entry.to_dict() for url, entry in self._cache.items()}
            
            with open(self.cache_file, 'w', encoding='utf-8') as f:
                json.dump(data, f, indent=2)
                
        except Exception as e:
            logger.error(f"Error saving cache: {e}")
    
    def _cleanup_expired(self):
        """Remove expired entries from cache"""
        expired = [url for url, entry in self._cache.items() if entry.is_expired()]
        for url in expired:
            del self._cache[url]
        if expired:
            logger.debug(f"Cleaned up {len(expired)} expired cache entries")
    
    def _normalize_url(self, url: str) -> str:
        """Normalize URL for cache key (domain only)"""
        try:
            from urllib.parse import urlparse
            parsed = urlparse(url)
            # Use domain as cache key
            return parsed.netloc.lower()
        except (ValueError, AttributeError) as e:
            logger.debug("URL parsing error: %s", e)
            return url.lower()
        except Exception:
            logger.exception("Unexpected error parsing URL")
            return url.lower()
    
    def get(self, url: str) -> Optional[CacheEntry]:
        """Get cached result for a URL"""
        key = self._normalize_url(url)
        entry = self._cache.get(key)
        
        if entry is None:
            return None
            
        if entry.is_expired():
            del self._cache[key]
            self._save_cache()
            return None
            
        return entry
    
    def set(self, url: str, score: int, risk_type: list, protective_action: int, ttl: int):
        """Cache a URL check result"""
        key = self._normalize_url(url)
        
        entry = CacheEntry(
            url=key,
            score=score,
            risk_type=risk_type,
            protective_action=protective_action,
            ttl=ttl,
            saved_at=time.time()
        )
        
        self._cache[key] = entry
        self._save_cache()
        
        logger.debug(f"Cached {key} with TTL {ttl}s")
    
    def has(self, url: str) -> bool:
        """Check if URL is in cache (and not expired)"""
        return self.get(url) is not None
    
    def remove(self, url: str):
        """Remove a URL from cache"""
        key = self._normalize_url(url)
        if key in self._cache:
            del self._cache[key]
            self._save_cache()
    
    def clear(self):
        """Clear all cache"""
        self._cache = {}
        self._save_cache()
        logger.info("Cache cleared")
    
    def get_stats(self) -> dict:
        """Get cache statistics"""
        self._cleanup_expired()
        return {
            "total_entries": len(self._cache),
            "cache_file": str(self.cache_file)
        }


# For standalone testing
if __name__ == "__main__":
    logging.basicConfig(level=logging.DEBUG)
    
    cache = CacheManager()
    
    print("Cache Stats:", cache.get_stats())
    
    # Test caching
    cache.set(
        url="https://example.com/page",
        score=80,
        risk_type=[0],
        protective_action=0,
        ttl=3600
    )
    
    result = cache.get("https://example.com/other-page")  # Same domain
    if result:
        print(f"\nCached result for example.com:")
        print(f"  Score: {result.score}")
        print(f"  Risk Type: {result.risk_type}")
        print(f"  Expires in: {result.ttl - (time.time() - result.saved_at):.0f}s")
