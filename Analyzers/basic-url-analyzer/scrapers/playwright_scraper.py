"""
Playwright-based web scraper
"""

import json
import re
from pathlib import Path
from typing import Dict
from playwright.sync_api import sync_playwright, TimeoutError as PlaywrightTimeout
from .base_scraper import BaseScraper
import sys
sys.path.append(str(Path(__file__).parent.parent))
from utils.logger import setup_logger


class PlaywrightScraper(BaseScraper):
    """Web scraper using Playwright"""
    
    def __init__(self):
        """Initialize Playwright scraper"""
        self.logger = setup_logger('playwright_scraper')
        
        # Load settings
        config_path = Path(__file__).parent.parent / 'config' / 'settings.json'
        with open(config_path, 'r') as f:
            settings = json.load(f)
        
        self.timeout = settings['scraping']['timeout_seconds'] * 1000  # Convert to ms
        self.user_agent = settings['scraping']['user_agent']
        self.headless = settings['scraping']['headless']
        
        self.playwright = None
        self.browser = None
    
    def fetch(self, url: str) -> Dict:
        """
        Fetch page content using Playwright
        
        Args:
            url: URL to fetch
        
        Returns:
            Result dictionary
        """
        try:
            self.logger.info(f"Fetching URL: {url}")
            
            # Start Playwright
            self.playwright = sync_playwright().start()
            self.browser = self.playwright.chromium.launch(
                headless=self.headless,
                args=[
                    '--disable-blink-features=AutomationControlled',
                    '--disable-dev-shm-usage',
                    '--no-sandbox',
                    '--disable-web-security',
                    '--disable-features=IsolateOrigins,site-per-process'
                ]
            )

            # Create context with anti-detection settings
            context = self.browser.new_context(
                user_agent=self.user_agent,
                viewport={'width': 1920, 'height': 1080},
                java_script_enabled=True,
                bypass_csp=True,
                ignore_https_errors=True,
                extra_http_headers={
                    'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8',
                    'Accept-Language': 'en-US,en;q=0.9',
                    'Accept-Encoding': 'gzip, deflate, br',
                    'Connection': 'keep-alive',
                    'Upgrade-Insecure-Requests': '1',
                    'Sec-Fetch-Dest': 'document',
                    'Sec-Fetch-Mode': 'navigate',
                    'Sec-Fetch-Site': 'none',
                    'Sec-Fetch-User': '?1',
                    'Cache-Control': 'max-age=0'
                }
            )

            # Create page
            page = context.new_page()

            # Hide webdriver property
            page.add_init_script("""
                Object.defineProperty(navigator, 'webdriver', {get: () => undefined});
                Object.defineProperty(navigator, 'plugins', {get: () => [1, 2, 3, 4, 5]});
                Object.defineProperty(navigator, 'languages', {get: () => ['en-US', 'en']});
                window.chrome = {runtime: {}};
            """)

            # Navigate to URL - try networkidle first with fallback
            try:
                # First try networkidle with shorter timeout (8s)
                response = page.goto(url, timeout=8000, wait_until='networkidle')
            except PlaywrightTimeout:
                # Fallback: reload with domcontentloaded if networkidle hangs
                self.logger.warning(f"networkidle timeout for {url}, falling back to domcontentloaded")
                response = page.goto(url, timeout=self.timeout, wait_until='domcontentloaded')
                # Give a bit more time for JS
                page.wait_for_timeout(2000)
            
            # Get final URL (after redirects)
            final_url = page.url
            
            # Get HTML content
            html = page.content()

            # Check for minimal content (potential JS rendering issue)
            text_content = re.sub(r'<[^>]+>', ' ', html)  # Strip HTML tags
            words = len(text_content.split())
            if words < 50:
                self.logger.warning(f"Minimal content extracted ({words} words) - JS may not have fully rendered")

            # Get status code
            status_code = response.status if response else 0

            self.logger.info(f"Successfully fetched {url} (status: {status_code}, words: {words})")
            
            return {
                'success': True,
                'html': html,
                'status_code': status_code,
                'final_url': final_url,
                'word_count': words,
                'error': ''
            }
        
        except PlaywrightTimeout:
            self.logger.warning(f"Timeout fetching {url}")
            return {
                'success': False,
                'html': '',
                'status_code': 0,
                'final_url': url,
                'word_count': 0,
                'error': 'Timeout - page took too long to load'
            }

        except Exception as e:
            self.logger.error(f"Error fetching {url}: {str(e)}")
            return {
                'success': False,
                'html': '',
                'status_code': 0,
                'final_url': url,
                'word_count': 0,
                'error': f"Scraping error: {str(e)}"
            }
        
        finally:
            self.close()
    
    def close(self) -> None:
        """Clean up Playwright resources"""
        try:
            if self.browser:
                self.browser.close()
            if self.playwright:
                self.playwright.stop()
        except Exception as e:
            self.logger.error(f"Error closing Playwright: {str(e)}")
