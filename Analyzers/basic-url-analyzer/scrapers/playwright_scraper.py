"""
Playwright-based web scraper
"""

import json
import re
import time
from pathlib import Path
from typing import Dict
from playwright.sync_api import sync_playwright, TimeoutError as PlaywrightTimeout
from .base_scraper import BaseScraper
import sys
sys.path.append(str(Path(__file__).parent.parent))
from utils.logger import setup_logger


# Blocked page indicators in title
BLOCKED_TITLE_PATTERNS = [
    "access denied", "403", "captcha", "just a moment",
    "checking your browser", "robot", "blocked", "forbidden",
    "attention required", "security check", "verify you are human",
    "please wait", "ddos protection"
]

# Fallback User-Agent strings for retry rotation
FALLBACK_USER_AGENTS = [
    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36",
    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:132.0) Gecko/20100101 Firefox/132.0",
    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Safari/605.1.15",
]

# Comprehensive anti-detection init script
ANTI_DETECTION_SCRIPT = """
    // Hide webdriver
    Object.defineProperty(navigator, 'webdriver', {get: () => undefined});
    delete navigator.__proto__.webdriver;

    // Realistic plugins array
    Object.defineProperty(navigator, 'plugins', {
        get: () => {
            const plugins = [
                {name: 'Chrome PDF Plugin', filename: 'internal-pdf-viewer', description: 'Portable Document Format'},
                {name: 'Chrome PDF Viewer', filename: 'mhjfbmdgcfjbbpaeojofohoefgiehjai', description: ''},
                {name: 'Native Client', filename: 'internal-nacl-plugin', description: ''}
            ];
            plugins.length = 3;
            return plugins;
        }
    });

    // Languages
    Object.defineProperty(navigator, 'languages', {get: () => ['en-US', 'en']});
    Object.defineProperty(navigator, 'language', {get: () => 'en-US'});

    // Platform
    Object.defineProperty(navigator, 'platform', {get: () => 'Win32'});

    // Hardware concurrency
    Object.defineProperty(navigator, 'hardwareConcurrency', {get: () => 8});

    // Device memory
    Object.defineProperty(navigator, 'deviceMemory', {get: () => 8});

    // Max touch points (0 for desktop)
    Object.defineProperty(navigator, 'maxTouchPoints', {get: () => 0});

    // Connection info
    Object.defineProperty(navigator, 'connection', {
        get: () => ({
            effectiveType: '4g',
            rtt: 50,
            downlink: 10,
            saveData: false
        })
    });

    // Chrome object
    window.chrome = {
        runtime: {
            connect: () => {},
            sendMessage: () => {},
            onMessage: {addListener: () => {}, removeListener: () => {}},
            onConnect: {addListener: () => {}, removeListener: () => {}}
        },
        loadTimes: () => ({
            commitLoadTime: Date.now() / 1000,
            connectionInfo: 'h2',
            finishDocumentLoadTime: Date.now() / 1000 + 0.5,
            finishLoadTime: Date.now() / 1000 + 1,
            firstPaintAfterLoadTime: 0,
            firstPaintTime: Date.now() / 1000 + 0.1,
            navigationType: 'Other',
            npnNegotiatedProtocol: 'h2',
            requestTime: Date.now() / 1000 - 0.5,
            startLoadTime: Date.now() / 1000 - 0.5,
            wasAlternateProtocolAvailable: false,
            wasFetchedViaSpdy: true,
            wasNpnNegotiated: true
        }),
        csi: () => ({
            onloadT: Date.now(),
            pageT: Date.now() - performance.timing.navigationStart,
            startE: performance.timing.navigationStart,
            tran: 15
        }),
        app: {
            isInstalled: false,
            InstallState: {DISABLED: 'disabled', INSTALLED: 'installed', NOT_INSTALLED: 'not_installed'},
            RunningState: {CANNOT_RUN: 'cannot_run', READY_TO_RUN: 'ready_to_run', RUNNING: 'running'}
        }
    };

    // Permissions API override
    const originalQuery = window.navigator.permissions?.query;
    if (originalQuery) {
        window.navigator.permissions.query = (parameters) => {
            if (parameters.name === 'notifications') {
                return Promise.resolve({state: Notification.permission});
            }
            return originalQuery(parameters);
        };
    }

    // WebGL vendor/renderer spoofing
    const getParameterProto = WebGLRenderingContext.prototype.getParameter;
    WebGLRenderingContext.prototype.getParameter = function(parameter) {
        if (parameter === 37445) return 'Google Inc. (NVIDIA)';  // UNMASKED_VENDOR_WEBGL
        if (parameter === 37446) return 'ANGLE (NVIDIA, NVIDIA GeForce GTX 1660 SUPER Direct3D11 vs_5_0 ps_5_0, D3D11)';  // UNMASKED_RENDERER_WEBGL
        return getParameterProto.call(this, parameter);
    };
    const getParameterProto2 = WebGL2RenderingContext.prototype.getParameter;
    WebGL2RenderingContext.prototype.getParameter = function(parameter) {
        if (parameter === 37445) return 'Google Inc. (NVIDIA)';
        if (parameter === 37446) return 'ANGLE (NVIDIA, NVIDIA GeForce GTX 1660 SUPER Direct3D11 vs_5_0 ps_5_0, D3D11)';
        return getParameterProto2.call(this, parameter);
    };

    // Screen properties
    Object.defineProperty(screen, 'width', {get: () => 1920});
    Object.defineProperty(screen, 'height', {get: () => 1080});
    Object.defineProperty(screen, 'availWidth', {get: () => 1920});
    Object.defineProperty(screen, 'availHeight', {get: () => 1040});
    Object.defineProperty(screen, 'colorDepth', {get: () => 24});
    Object.defineProperty(screen, 'pixelDepth', {get: () => 24});

    // Window dimensions
    Object.defineProperty(window, 'outerWidth', {get: () => 1920});
    Object.defineProperty(window, 'outerHeight', {get: () => 1080});
    Object.defineProperty(window, 'innerWidth', {get: () => 1903});
    Object.defineProperty(window, 'innerHeight', {get: () => 969});

    // Prevent iframe detection
    Object.defineProperty(window, 'self', {get: () => window});
    Object.defineProperty(window, 'top', {get: () => window});

    // Hide automation-related properties
    const automationProps = [
        '_selenium', '__webdriver_script_fn', '__driver_evaluate',
        '__webdriver_evaluate', '__fxdriver_evaluate', '__driver_unwrapped',
        '__webdriver_unwrapped', '__fxdriver_unwrapped', '_Selenium_IDE_Recorder',
        'calledSelenium', '_phantom', '__nightmare', 'domAutomation',
        'domAutomationController'
    ];
    automationProps.forEach(prop => {
        Object.defineProperty(window, prop, {get: () => undefined});
    });

    // Notification permission
    if (!window.Notification) {
        window.Notification = {permission: 'default'};
    }
"""


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

    def _is_blocked(self, page) -> bool:
        """Check if the page appears to be blocked/captcha'd"""
        try:
            title = (page.title() or "").lower()
            for pattern in BLOCKED_TITLE_PATTERNS:
                if pattern in title:
                    return True
            return False
        except Exception:
            return False

    def _fetch_with_ua(self, url: str, user_agent: str) -> Dict:
        """
        Attempt to fetch a URL with a specific User-Agent.
        Returns the result dict. Caller manages playwright lifecycle.
        """
        context = self.browser.new_context(
            user_agent=user_agent,
            viewport={'width': 1920, 'height': 1080},
            java_script_enabled=True,
            bypass_csp=True,
            ignore_https_errors=True,
            locale='en-US',
            timezone_id='America/New_York',
            extra_http_headers={
                'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
                'Accept-Language': 'en-US,en;q=0.9',
                'Accept-Encoding': 'gzip, deflate, br, zstd',
                'Connection': 'keep-alive',
                'Upgrade-Insecure-Requests': '1',
                'Sec-Fetch-Dest': 'document',
                'Sec-Fetch-Mode': 'navigate',
                'Sec-Fetch-Site': 'none',
                'Sec-Fetch-User': '?1',
                'Cache-Control': 'max-age=0',
                'Sec-Ch-Ua': '"Chromium";v="131", "Not_A Brand";v="24"',
                'Sec-Ch-Ua-Mobile': '?0',
                'Sec-Ch-Ua-Platform': '"Windows"'
            }
        )

        page = context.new_page()
        page.add_init_script(ANTI_DETECTION_SCRIPT)

        # Navigate to URL - try networkidle first with fallback
        try:
            response = page.goto(url, timeout=8000, wait_until='networkidle')
        except PlaywrightTimeout:
            self.logger.warning(f"networkidle timeout for {url}, falling back to domcontentloaded")
            response = page.goto(url, timeout=self.timeout, wait_until='domcontentloaded')
            page.wait_for_timeout(2000)

        final_url = page.url
        html = page.content()

        text_content = re.sub(r'<[^>]+>', ' ', html)
        words = len(text_content.split())
        if words < 50:
            self.logger.warning(f"Minimal content extracted ({words} words) - JS may not have fully rendered")

        status_code = response.status if response else 0
        blocked = self._is_blocked(page)

        context.close()

        return {
            'success': True,
            'html': html,
            'status_code': status_code,
            'final_url': final_url,
            'word_count': words,
            'error': '',
            '_blocked': blocked
        }

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
                    '--disable-features=IsolateOrigins,site-per-process',
                    '--disable-infobars',
                    '--window-size=1920,1080',
                    '--disable-extensions',
                    '--disable-plugins-discovery',
                    '--disable-background-timer-throttling',
                    '--disable-backgrounding-occluded-windows',
                    '--disable-renderer-backgrounding'
                ]
            )

            # First attempt with primary User-Agent
            result = self._fetch_with_ua(url, self.user_agent)

            # If blocked, retry once with a different User-Agent
            if result.get('_blocked'):
                self.logger.warning(f"Blocked on first attempt for {url}, retrying with different User-Agent")
                time.sleep(2)

                # Pick a fallback UA different from the primary
                import random
                fallback_ua = random.choice(FALLBACK_USER_AGENTS)
                result = self._fetch_with_ua(url, fallback_ua)

                if result.get('_blocked'):
                    self.logger.warning(f"Still blocked on retry for {url}, returning result as-is")

            # Remove internal _blocked key before returning
            result.pop('_blocked', None)

            self.logger.info(f"Successfully fetched {url} (status: {result['status_code']}, words: {result['word_count']})")
            return result

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
