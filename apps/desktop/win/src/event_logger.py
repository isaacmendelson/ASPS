"""
AntiScam Desktop App - Event Logger
Logs events to JSON lines file
"""

import os
import json
from datetime import datetime
from typing import Any, Dict, Optional
from pathlib import Path
import logging

from config import DATA_DIR, LOG_FILE

logger = logging.getLogger(__name__)


class EventLogger:
    """Logs events to a JSON lines file"""
    
    def __init__(self):
        self.data_dir = Path(os.path.expanduser(DATA_DIR))
        self.log_file = self.data_dir / LOG_FILE
        self._ensure_data_dir()
        
    def _ensure_data_dir(self):
        """Create data directory if it doesn't exist"""
        self.data_dir.mkdir(parents=True, exist_ok=True)
    
    def _get_timestamp(self) -> str:
        """Get current timestamp"""
        return datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    
    def log(self, event_type: str, data: Dict[str, Any], direction: str = ""):
        """
        Log an event
        
        Args:
            event_type: Type of event (e.g., 'SuspiciousUrlAlert', 'RemoteAccessAlert')
            data: Event data
            direction: 'sent', 'received', or empty
        """
        entry = {
            "timestamp": self._get_timestamp(),
            "type": event_type,
            "direction": direction,
            "data": data
        }
        
        try:
            with open(self.log_file, 'a', encoding='utf-8') as f:
                f.write(json.dumps(entry, ensure_ascii=False) + '\n')
        except Exception as e:
            logger.error(f"Error writing to event log: {e}")
    
    def log_sent(self, event_type: str, data: Dict[str, Any]):
        """Log a sent message"""
        self.log(event_type, data, direction="sent")
    
    def log_received(self, event_type: str, data: Dict[str, Any]):
        """Log a received message"""
        self.log(event_type, data, direction="received")
    
    def log_event(self, event_type: str, data: Dict[str, Any]):
        """Log a local event"""
        self.log(event_type, data, direction="local")
    
    def log_error(self, error: str, context: Optional[Dict[str, Any]] = None):
        """Log an error"""
        data = {"error": error}
        if context:
            data["context"] = context
        self.log("Error", data, direction="local")
    
    def get_recent_logs(self, count: int = 100) -> list:
        """Get recent log entries"""
        if not self.log_file.exists():
            return []
            
        try:
            with open(self.log_file, 'r', encoding='utf-8') as f:
                lines = f.readlines()
                
            # Get last N lines
            recent_lines = lines[-count:] if len(lines) > count else lines
            
            entries = []
            for line in recent_lines:
                try:
                    entries.append(json.loads(line.strip()))
                except json.JSONDecodeError as e:
                    logger.warning("Failed to parse event JSON: %s", e)
                    continue
                except Exception:
                    logger.exception("Unexpected error parsing event")
                    continue

            return entries
            
        except Exception as e:
            logger.error(f"Error reading event log: {e}")
            return []
    
    def get_logs_by_type(self, event_type: str, count: int = 50) -> list:
        """Get logs filtered by type"""
        all_logs = self.get_recent_logs(1000)  # Read more to filter
        filtered = [l for l in all_logs if l.get('type') == event_type]
        return filtered[-count:]
    
    def clear_old_logs(self, days: int = 30):
        """Clear logs older than specified days"""
        if not self.log_file.exists():
            return
            
        try:
            cutoff = datetime.now().timestamp() - (days * 24 * 3600)
            
            with open(self.log_file, 'r', encoding='utf-8') as f:
                lines = f.readlines()
            
            kept_lines = []
            for line in lines:
                try:
                    entry = json.loads(line.strip())
                    entry_time = datetime.strptime(
                        entry['timestamp'],
                        "%Y-%m-%d %H:%M:%S"
                    ).timestamp()

                    if entry_time > cutoff:
                        kept_lines.append(line)
                except json.JSONDecodeError as e:
                    logger.warning("Failed to parse log line: %s", e)
                    continue
                except Exception:
                    logger.exception("Unexpected error reading log")
                    continue

            with open(self.log_file, 'w', encoding='utf-8') as f:
                f.writelines(kept_lines)
                
            removed = len(lines) - len(kept_lines)
            if removed > 0:
                logger.info(f"Cleared {removed} old log entries")
                
        except Exception as e:
            logger.error(f"Error clearing old logs: {e}")
    
    def get_stats(self) -> dict:
        """Get logging statistics"""
        if not self.log_file.exists():
            return {"total_entries": 0, "file_size": 0}
            
        try:
            file_size = self.log_file.stat().st_size
            with open(self.log_file, 'r', encoding='utf-8') as f:
                line_count = sum(1 for _ in f)
                
            return {
                "total_entries": line_count,
                "file_size": file_size,
                "file_path": str(self.log_file)
            }
        except OSError as e:
            logger.debug("Could not get file stats: %s", e)
            return {"total_entries": 0, "file_size": 0}
        except Exception:
            logger.exception("Unexpected error getting file stats")
            return {"total_entries": 0, "file_size": 0}


# For standalone testing
if __name__ == "__main__":
    logging.basicConfig(level=logging.DEBUG)
    
    event_logger = EventLogger()
    
    # Test logging
    event_logger.log_sent("SuspiciousUrlAlert", {
        "url": "https://suspicious-site.com",
        "score": 25
    })
    
    event_logger.log_received("SuspiciousUrlAlertResponse", {
        "score": 25,
        "riskType": [1, 2],
        "protectiveAction": 3
    })
    
    event_logger.log_event("RemoteAccessDetected", {
        "app": "anydesk",
        "status": "active_session"
    })
    
    print("\nRecent logs:")
    print("=" * 50)
    for entry in event_logger.get_recent_logs(10):
        print(json.dumps(entry, indent=2))
    
    print("\nStats:", event_logger.get_stats())
