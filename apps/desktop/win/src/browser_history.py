"""
AntiScam Desktop App - Browser History Monitor
Reads browser history directly from SQLite files
"""

import os
import json
import sqlite3
import shutil
import tempfile
import platform
import glob
import time
from contextlib import contextmanager
from datetime import datetime, timedelta
from pathlib import Path
from typing import List, Dict, Optional, Set, Generator, Union
from dataclasses import dataclass
import logging

from config import BROWSER_HISTORY, DATA_DIR

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

    DISCOVERED = "discovered"
    QUEUED = "queued"
    SENT = "sent"
    ACKNOWLEDGED = "acknowledged"
    FAILED = "failed"
    MAX_ACKNOWLEDGED_RECORDS = 5000

    def __init__(self, state_file: Optional[Union[str, Path]] = None):
        self.system = platform.system()
        self._state_file = Path(
            state_file or Path(os.path.expanduser(DATA_DIR)) / "browser-history-delivery.json"
        )
        self._delivery_state: Dict[str, Dict] = {}
        self._seen_urls: Set[str] = set()  # Acknowledged 'browser:url' keys (index into _delivery_state)
        self._last_check: Dict[str, datetime] = {}  # Last check time per browser
        self._load_delivery_state()

    @staticmethod
    def _url_key(url: str, browser: str) -> str:
        return f"{browser}:{url}"

    def _load_delivery_state(self) -> None:
        """Load durable delivery state and recover interrupted sends for retry."""
        if not self._state_file.exists():
            return

        try:
            with self._state_file.open("r", encoding="utf-8") as state_stream:
                raw_state = json.load(state_stream)
            if not isinstance(raw_state, dict):
                raise ValueError("delivery state must be a JSON object")

            recovered_sent = False
            for key, record in raw_state.items():
                if not isinstance(record, dict):
                    continue
                status = record.get("status")
                if status not in {
                    self.DISCOVERED, self.QUEUED, self.SENT,
                    self.ACKNOWLEDGED, self.FAILED,
                }:
                    continue
                if status == self.SENT:
                    record["status"] = self.FAILED
                    recovered_sent = True
                elif status == self.DISCOVERED:
                    record["status"] = self.QUEUED
                    recovered_sent = True
                self._delivery_state[key] = record

            self._refresh_seen_index()
            if recovered_sent:
                self._save_delivery_state()
        except (OSError, ValueError, TypeError, json.JSONDecodeError) as error:
            logger.warning("Cannot load browser-history delivery state %s: %s",
                           self._state_file, error)
            self._delivery_state = {}
            self._seen_urls = set()

    def _save_delivery_state(self) -> None:
        """Persist state atomically so a restart cannot observe partial JSON."""
        try:
            self._prune_acknowledged_state()
            self._state_file.parent.mkdir(parents=True, exist_ok=True)
            temp_file = self._state_file.with_suffix(self._state_file.suffix + ".tmp")
            with temp_file.open("w", encoding="utf-8") as state_stream:
                json.dump(self._delivery_state, state_stream, indent=2, sort_keys=True)
            os.replace(temp_file, self._state_file)
        except OSError as error:
            logger.error("Cannot save browser-history delivery state %s: %s",
                         self._state_file, error)

    def _prune_acknowledged_state(self) -> None:
        """Bound completed dedup state without ever dropping pending retries."""
        acknowledged = [
            (key, record) for key, record in self._delivery_state.items()
            if record.get("status") == self.ACKNOWLEDGED
        ]
        overflow = len(acknowledged) - self.MAX_ACKNOWLEDGED_RECORDS
        if overflow <= 0:
            return
        acknowledged.sort(key=lambda item: item[1].get("updated_at", 0))
        for key, _ in acknowledged[:overflow]:
            del self._delivery_state[key]
        self._refresh_seen_index()

    def _refresh_seen_index(self) -> None:
        """Rebuild the fast-lookup index of acknowledged 'browser:url' keys."""
        self._seen_urls = {
            key for key, record in self._delivery_state.items()
            if record.get("status") == self.ACKNOWLEDGED
        }

    def _record_entry(self, entry: HistoryEntry) -> None:
        key = self._url_key(entry.url, entry.browser)
        if key in self._delivery_state:
            return
        self._delivery_state[key] = {
            "status": self.QUEUED,
            "url": entry.url,
            "browser": entry.browser,
            "title": entry.title,
            "visit_time": entry.visit_time.isoformat(),
            "visit_count": entry.visit_count,
            "updated_at": time.time(),
        }

    @staticmethod
    def _entry_from_record(record: Dict) -> Optional[HistoryEntry]:
        try:
            return HistoryEntry(
                url=record["url"],
                title=record.get("title", ""),
                visit_time=datetime.fromisoformat(record["visit_time"]),
                browser=record["browser"],
                visit_count=int(record.get("visit_count", 1)),
            )
        except (KeyError, TypeError, ValueError):
            return None
        
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
        """Discover entries and return durable queued/failed delivery candidates."""
        # Get entries from last 5 minutes
        since = datetime.now() - timedelta(minutes=5)
        all_entries = self.get_all_history(since)

        state_changed = False
        for entry in all_entries:
            url_key = self._url_key(entry.url, entry.browser)
            if url_key not in self._delivery_state and not self.is_url_seen(entry.url):
                self._record_entry(entry)
                state_changed = True

        if state_changed:
            self._save_delivery_state()

        candidates = []
        candidate_urls = set()
        for record in self._delivery_state.values():
            if record.get("status") not in (self.QUEUED, self.FAILED):
                continue
            url = record.get("url")
            if not url or url in candidate_urls or self.is_url_seen(url):
                continue
            entry = self._entry_from_record(record)
            if entry:
                candidates.append(entry)
                candidate_urls.add(url)

        candidates.sort(key=lambda entry: entry.visit_time, reverse=True)
        return candidates

    def queue_url_for_delivery(self, url: str, browser: str) -> None:
        """Durably queue a URL without treating discovery as acknowledgement."""
        key = self._url_key(url, browser)
        if key in self._delivery_state or self.is_url_seen(url):
            return
        self._delivery_state[key] = {
            "status": self.QUEUED,
            "url": url,
            "browser": browser,
            "title": "",
            "visit_time": datetime.now().isoformat(),
            "visit_count": 1,
            "updated_at": time.time(),
        }
        self._save_delivery_state()

    def _set_delivery_status(self, url: str, browser: str, status: str) -> None:
        key = self._url_key(url, browser)
        record = self._delivery_state.get(key)
        if record is None:
            record = {
                "url": url,
                "browser": browser,
                "title": "",
                "visit_time": datetime.now().isoformat(),
                "visit_count": 1,
                "updated_at": time.time(),
            }
            self._delivery_state[key] = record
        record["status"] = status
        record["updated_at"] = time.time()
        self._refresh_seen_index()
        self._save_delivery_state()

    def mark_delivery_sent(self, url: str, browser: str) -> None:
        self._set_delivery_status(url, browser, self.SENT)

    def mark_delivery_acknowledged(self, url: str, browser: str) -> None:
        # Equivalent discoveries from other browsers represent the same backend
        # URL delivery and are acknowledged together.
        matching_keys = [
            key for key, record in self._delivery_state.items()
            if record.get("url") == url
        ]
        if not matching_keys:
            self._set_delivery_status(url, browser, self.ACKNOWLEDGED)
            return
        for key in matching_keys:
            self._delivery_state[key]["status"] = self.ACKNOWLEDGED
            self._delivery_state[key]["updated_at"] = time.time()
        self._refresh_seen_index()
        self._save_delivery_state()

    def mark_delivery_failed(self, url: str, browser: str) -> None:
        self._set_delivery_status(url, browser, self.FAILED)

    def get_delivery_state(self, url: str, browser: str) -> Optional[str]:
        record = self._delivery_state.get(self._url_key(url, browser))
        return record.get("status") if record else None
    
    def is_url_seen(self, url: str) -> bool:
        """Return True only after the backend acknowledged delivery."""
        return any(key.endswith(f":{url}") for key in self._seen_urls)


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
