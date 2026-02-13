"""
Base abstract scraper interface
"""

from abc import ABC, abstractmethod
from typing import Dict


class BaseScraper(ABC):
    """Abstract base class for web scrapers"""
    
    @abstractmethod
    def fetch(self, url: str) -> Dict:
        """
        Fetch content from URL
        
        Args:
            url: URL to fetch
        
        Returns:
            Dictionary with:
            {
                'success': bool,
                'html': str,
                'status_code': int,
                'final_url': str,
                'error': str
            }
        """
        pass
    
    @abstractmethod
    def close(self) -> None:
        """Clean up resources"""
        pass
