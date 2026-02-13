"""
AntiScam Desktop App - Browser History Monitor
Reads browser history directly from SQLite files
"""

import os
import sqlite3
import shutil
import tempfile
import platform
import glob
from contextlib import contextmanager
from datetime import datetime, timedelta
from typing import List, Dict, Optional, Set, Generator
from dataclasses import dataclass
import logging

from config import BROWSER_HISTORY

logger = logging.getLogger(__name__)


@contextmanager
def temp_database_copy(source_path: str) -> Generator[str, None, None]:
    """Create a temporary copy of a database file with guaranteed cleanup.

    Windows file locking requires special handling:
    - Source database may be locked by browser
    - Temp file must be deleted after SQLite connection closes

    Args:
        source_path: Path to the source database file

    Yields:
        Path to the temporary copy

    Raises:
        FileNotFoundError: If source database doesn't exist
        OSError: If copy operation fails
    """
    temp_path = None
    try:
        # Create temp file
        fd, temp_path = tempfile.mkstemp(suffix='.db')
        os.close(fd)  # Close file descriptor, we'll use the path

        # Copy source to temp
        shutil.copy2(source_path, temp_path)

        yield temp_path

    finally:
        # Cleanup temp file - must happen after caller closes any connections
        if temp_path and os.path.exists(temp_path):
            try:
                os.remove(temp_path)
            except OSError as e:
                logger.warning("Failed to remove temp file %s: %s", temp_path, e)


@dataclass
class HistoryEntry:
    """A single browser history entry"""
    url: str
    title: str
    visit_time: datetime
    browser: str
    visit_count: int = 1


class BrowserHistoryMonitor:
    """Monitor browser history across multiple browsers"""
    
    def __init__(self):
        self.system = platform.system()
        self._seen_urls: Set[str] = set()  # Track URLs we've already processed
        self._last_check: Dict[str, datetime] = {}  # Last check time per browser
        
    def _get_history_path(self, browser: str) -> Optional[str]:
        """Get history file path for a browser"""
        browser_config = BROWSER_HISTORY.get(browser)
        if not browser_config:
            return None
            
        if self.system == "Windows":
            path = browser_config.get('windows')
        elif self.system == "Linux":
            path = browser_config.get('linux')
        elif self.system == "Darwin":
            path = browser_config.get('mac')
        else:
            return None
            
        if path:
            return os.path.expanduser(path)
        return None

    def _chrome_time_to_datetime(self, chrome_time: int) -> datetime:
        """Convert Chrome timestamp to datetime"""
        # Chrome uses microseconds since Jan 1, 1601
        epoch_start = datetime(1601, 1, 1)
        return epoch_start + timedelta(microseconds=chrome_time)
    
    def _firefox_time_to_datetime(self, firefox_time: int) -> datetime:
        """Convert Firefox timestamp to datetime"""
        # Firefox uses microseconds since Unix epoch
        return datetime.fromtimestamp(firefox_time / 1000000)
    
    def get_chrome_history(self, since: Optional[datetime] = None) -> List[HistoryEntry]:
        """Get Chrome browser history"""
        history_path = self._get_history_path('chrome')
        if not history_path or not os.path.exists(history_path):
            return []

        entries = []
        try:
            with temp_database_copy(history_path) as temp_db:
                conn = sqlite3.connect(temp_db)
                try:
                    cursor = conn.cursor()

                    query = """
                        SELECT url, title, visit_count, last_visit_time
                        FROM urls
                        ORDER BY last_visit_time DESC
                        LIMIT 100
                    """

                    cursor.execute(query)

                    for row in cursor.fetchall():
                        url, title, visit_count, last_visit = row
                        visit_time = self._chrome_time_to_datetime(last_visit)

                        # Filter by time if specified
                        if since and visit_time < since:
                            continue

                        entries.append(HistoryEntry(
                            url=url,
                            title=title or "",
                            visit_time=visit_time,
                            browser="chrome",
                            visit_count=visit_count
                        ))

                except sqlite3.OperationalError as e:
                    logger.warning("Database operational error reading Chrome history: %s", e)
                except sqlite3.DatabaseError as e:
                    logger.warning("Database error reading Chrome history: %s", e)
                finally:
                    conn.close()

        except FileNotFoundError:
            logger.debug("Chrome history database not found: %s", history_path)
        except (OSError, PermissionError) as e:
            logger.warning("Cannot access Chrome history database %s: %s", history_path, e)

        return entries
    
    def get_edge_history(self, since: Optional[datetime] = None) -> List[HistoryEntry]:
        """Get Edge browser history (same format as Chrome)"""
        history_path = self._get_history_path('edge')
        if not history_path or not os.path.exists(history_path):
            return []

        entries = []
        try:
            with temp_database_copy(history_path) as temp_db:
                conn = sqlite3.connect(temp_db)
                try:
                    cursor = conn.cursor()

                    query = """
                        SELECT url, title, visit_count, last_visit_time
                        FROM urls
                        ORDER BY last_visit_time DESC
                        LIMIT 100
                    """

                    cursor.execute(query)

                    for row in cursor.fetchall():
                        url, title, visit_count, last_visit = row
                        visit_time = self._chrome_time_to_datetime(last_visit)

                        if since and visit_time < since:
                            continue

                        entries.append(HistoryEntry(
                            url=url,
                            title=title or "",
                            visit_time=visit_time,
                            browser="edge",
                            visit_count=visit_count
                        ))

                except sqlite3.OperationalError as e:
                    logger.warning("Database operational error reading Edge history: %s", e)
                except sqlite3.DatabaseError as e:
                    logger.warning("Database error reading Edge history: %s", e)
                finally:
                    conn.close()

        except FileNotFoundError:
            logger.debug("Edge history database not found: %s", history_path)
        except (OSError, PermissionError) as e:
            logger.warning("Cannot access Edge history database %s: %s", history_path, e)

        return entries
    
    def get_firefox_history(self, since: Optional[datetime] = None) -> List[HistoryEntry]:
        """Get Firefox browser history"""
        profiles_path = self._get_history_path('firefox')
        if not profiles_path:
            return []

        # Find Firefox profile with places.sqlite
        pattern = os.path.join(profiles_path, "*.default*", "places.sqlite")
        matches = glob.glob(pattern)

        if not matches:
            return []

        history_path = matches[0]
        if not os.path.exists(history_path):
            return []

        entries = []
        try:
            with temp_database_copy(history_path) as temp_db:
                conn = sqlite3.connect(temp_db)
                try:
                    cursor = conn.cursor()

                    query = """
                        SELECT p.url, p.title, p.visit_count, h.visit_date
                        FROM moz_places p
                        JOIN moz_historyvisits h ON p.id = h.place_id
                        ORDER BY h.visit_date DESC
                        LIMIT 100
                    """

                    cursor.execute(query)

                    for row in cursor.fetchall():
                        url, title, visit_count, visit_date = row
                        visit_time = self._firefox_time_to_datetime(visit_date)

                        if since and visit_time < since:
                            continue

                        entries.append(HistoryEntry(
                            url=url,
                            title=title or "",
                            visit_time=visit_time,
                            browser="firefox",
                            visit_count=visit_count or 1
                        ))

                except sqlite3.OperationalError as e:
                    logger.warning("Database operational error reading Firefox history: %s", e)
                except sqlite3.DatabaseError as e:
                    logger.warning("Database error reading Firefox history: %s", e)
                finally:
                    conn.close()

        except FileNotFoundError:
            logger.debug("Firefox history database not found: %s", history_path)
        except (OSError, PermissionError) as e:
            logger.warning("Cannot access Firefox history database %s: %s", history_path, e)

        return entries
    
    def get_all_history(self, since: Optional[datetime] = None) -> List[HistoryEntry]:
        """Get history from all browsers"""
        all_entries = []
        
        all_entries.extend(self.get_chrome_history(since))
        all_entries.extend(self.get_edge_history(since))
        all_entries.extend(self.get_firefox_history(since))
        
        # Sort by visit time, newest first
        all_entries.sort(key=lambda x: x.visit_time, reverse=True)
        
        return all_entries
    
    def get_new_entries(self) -> List[HistoryEntry]:
        """Get only new entries since last check"""
        # Get entries from last 5 minutes
        since = datetime.now() - timedelta(minutes=5)
        all_entries = self.get_all_history(since)
        
        # Filter out already seen URLs
        new_entries = []
        for entry in all_entries:
            url_key = f"{entry.browser}:{entry.url}"
            if url_key not in self._seen_urls:
                self._seen_urls.add(url_key)
                new_entries.append(entry)
                
        # Limit seen URLs cache size
        if len(self._seen_urls) > 10000:
            self._seen_urls = set(list(self._seen_urls)[-5000:])
            
        return new_entries
    
    def mark_url_as_sent(self, url: str, browser: str):
        """Mark a URL as already sent to server"""
        url_key = f"{browser}:{url}"
        self._seen_urls.add(url_key)
    
    def is_url_seen(self, url: str) -> bool:
        """Check if we've already processed this URL"""
        for browser in ['chrome', 'edge', 'firefox']:
            if f"{browser}:{url}" in self._seen_urls:
                return True
        return False


# For standalone testing
if __name__ == "__main__":
    logging.basicConfig(level=logging.DEBUG)
    
    monitor = BrowserHistoryMonitor()
    
    print("Reading browser history...")
    print("=" * 50)
    
    # Get last hour
    since = datetime.now() - timedelta(hours=1)
    entries = monitor.get_all_history(since)
    
    print(f"\nFound {len(entries)} entries in the last hour:\n")
    
    for entry in entries[:20]:  # Show first 20
        print(f"[{entry.browser}] {entry.visit_time.strftime('%H:%M:%S')}")
        print(f"  {entry.title[:50] if entry.title else 'No title'}")
        print(f"  {entry.url[:80]}")
        print()
