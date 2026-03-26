import { describe, test, expect, beforeEach, jest } from '@jest/globals';

describe('TrackerService', () => {
  let trackerService;

  beforeEach(async () => {
    // Mock DOM
    document.body.innerHTML = '';
    
    // Mock window.location
    delete window.location;
    window.location = {
      href: 'https://example.com/page',
      hostname: 'example.com'
    };

    // Import module
    const module = await import('@/services/TrackerService.js');
    trackerService = module.trackerService;
  });

  describe('Facebook Pixel Detection', () => {
    test('should detect FB pixel from script tag', () => {
      document.body.innerHTML = `
        <script>
          fbq('init', '123456789');
        </script>
      `;

      const pixels = trackerService.findFacebookPixels();

      expect(pixels.length).toBe(1);
      expect(pixels[0]).toEqual({
        Type: 'fbPixel',
        Value: '123456789',
        Source: 'script'
      });
    });

    test('should detect FB pixel from noscript tag', () => {
      document.body.innerHTML = `
        <noscript>
          <img src="https://www.facebook.com/tr?id=987654321&ev=PageView" />
        </noscript>
      `;

      const pixels = trackerService.findFacebookPixels();

      expect(pixels.length).toBe(1);
      expect(pixels[0]).toEqual({
        Type: 'fbPixel',
        Value: '987654321',
        Source: 'noscript'
      });
    });

    test('should detect multiple FB pixels', () => {
      document.body.innerHTML = `
        <script>
          fbq('init', '111111111');
          fbq('init', '222222222');
        </script>
      `;

      const pixels = trackerService.findFacebookPixels();

      expect(pixels.length).toBe(2);
      expect(pixels.map(p => p.Value)).toContain('111111111');
      expect(pixels.map(p => p.Value)).toContain('222222222');
    });

    test('should deduplicate identical pixels', () => {
      document.body.innerHTML = `
        <script>
          fbq('init', '123456789');
        </script>
        <noscript>
          <img src="https://www.facebook.com/tr?id=123456789&ev=PageView" />
        </noscript>
      `;

      const pixels = trackerService.findFacebookPixels();

      expect(pixels.length).toBe(1);
      expect(pixels[0].Value).toBe('123456789');
    });

    test('should handle pages without FB pixels', () => {
      document.body.innerHTML = '<div>No trackers here</div>';

      const pixels = trackerService.findFacebookPixels();

      expect(pixels.length).toBe(0);
    });
  });

  describe('Google Analytics Detection', () => {
    test('should detect GA4 from script tag', () => {
      document.body.innerHTML = `
        <script>
          gtag('config', 'G-ABCD1234');
        </script>
      `;

      const trackers = trackerService.findGoogleAnalytics();

      expect(trackers.length).toBe(1);
      expect(trackers[0]).toEqual({
        Type: 'ga4',
        Value: 'G-ABCD1234',
        Source: 'script'
      });
    });

    test('should detect GA4 from script src', () => {
      const script = document.createElement('script');
      script.src = 'https://www.googletagmanager.com/gtag/js?id=G-XYZA9999';
      document.body.appendChild(script);

      const trackers = trackerService.findGoogleAnalytics();

      expect(trackers.length).toBe(1);
      expect(trackers[0].Value).toBe('G-XYZA9999');
    });

    test('should detect Universal Analytics (UA)', () => {
      document.body.innerHTML = `
        <script>
          ga('create', 'UA-12345-6', 'auto');
        </script>
      `;

      const trackers = trackerService.findGoogleAnalytics();

      expect(trackers.length).toBe(1);
      expect(trackers[0]).toEqual({
        Type: 'ua',
        Value: 'UA-12345-6',
        Source: 'script'
      });
    });

    test('should detect multiple GA trackers', () => {
      document.body.innerHTML = `
        <script>
          gtag('config', 'G-FIRST123');
          ga('create', 'UA-99999-1', 'auto');
        </script>
      `;

      const trackers = trackerService.findGoogleAnalytics();

      expect(trackers.length).toBe(2);
      expect(trackers.some(t => t.Value === 'G-FIRST123')).toBe(true);
      expect(trackers.some(t => t.Value === 'UA-99999-1')).toBe(true);
    });

    test('should deduplicate identical GA trackers', () => {
      document.body.innerHTML = `
        <script>
          gtag('config', 'G-SAME123');
          gtag('event', 'page_view', { send_to: 'G-SAME123' });
        </script>
      `;

      const trackers = trackerService.findGoogleAnalytics();

      expect(trackers.length).toBe(1);
    });
  });

  describe('External iFrame Detection', () => {
    test('should detect external iframes', () => {
      document.body.innerHTML = `
        <iframe src="https://external-site.com/widget"></iframe>
      `;

      const iframes = trackerService.findExternalIframes();

      expect(iframes).toContain('external-site.com');
    });

    test('should ignore same-domain iframes', () => {
      document.body.innerHTML = `
        <iframe src="https://example.com/internal"></iframe>
      `;

      const iframes = trackerService.findExternalIframes();

      expect(iframes.length).toBe(0);
    });

    test('should detect multiple external iframes', () => {
      document.body.innerHTML = `
        <iframe src="https://ads.example.net/banner"></iframe>
        <iframe src="https://tracking.example.org/pixel"></iframe>
      `;

      const iframes = trackerService.findExternalIframes();

      expect(iframes.length).toBe(2);
      expect(iframes).toContain('ads.example.net');
      expect(iframes).toContain('tracking.example.org');
    });

    test('should deduplicate identical iframe domains', () => {
      document.body.innerHTML = `
        <iframe src="https://ads.example.net/banner1"></iframe>
        <iframe src="https://ads.example.net/banner2"></iframe>
      `;

      const iframes = trackerService.findExternalIframes();

      expect(iframes.length).toBe(1);
      expect(iframes[0]).toBe('ads.example.net');
    });

    test('should handle invalid iframe URLs', () => {
      document.body.innerHTML = `
        <iframe src="invalid-url"></iframe>
      `;

      const iframes = trackerService.findExternalIframes();

      expect(iframes.length).toBe(0);
    });
  });

  describe('Third-Party Script Detection', () => {
    test('should detect external scripts', () => {
      const script = document.createElement('script');
      script.src = 'https://cdn.thirdparty.com/analytics.js';
      document.body.appendChild(script);

      const scripts = trackerService.findThirdPartyScripts();

      expect(scripts.length).toBe(1);
      expect(scripts[0].domain).toBe('cdn.thirdparty.com');
      expect(scripts[0].src).toBe('https://cdn.thirdparty.com/analytics.js');
    });

    test('should ignore same-domain scripts', () => {
      const script = document.createElement('script');
      script.src = 'https://example.com/local.js';
      document.body.appendChild(script);

      const scripts = trackerService.findThirdPartyScripts();

      expect(scripts.length).toBe(0);
    });

    test('should detect multiple third-party scripts', () => {
      const script1 = document.createElement('script');
      script1.src = 'https://cdn1.example.net/script1.js';
      document.body.appendChild(script1);

      const script2 = document.createElement('script');
      script2.src = 'https://cdn2.example.org/script2.js';
      document.body.appendChild(script2);

      const scripts = trackerService.findThirdPartyScripts();

      expect(scripts.length).toBe(2);
      expect(scripts.map(s => s.domain)).toContain('cdn1.example.net');
      expect(scripts.map(s => s.domain)).toContain('cdn2.example.org');
    });

    test('should ignore inline scripts', () => {
      document.body.innerHTML = `
        <script>console.log('inline');</script>
      `;

      const scripts = trackerService.findThirdPartyScripts();

      expect(scripts.length).toBe(0);
    });
  });

  describe('Page Info Collection', () => {
    test('should collect comprehensive page info', () => {
      document.body.innerHTML = `
        <script>fbq('init', '123456789');</script>
        <script>gtag('config', 'G-ABCD1234');</script>
        <iframe src="https://ads.example.net/banner"></iframe>
      `;

      const script = document.createElement('script');
      script.src = 'https://cdn.thirdparty.com/tracker.js';
      document.body.appendChild(script);

      const info = trackerService.getPageInfo();

      expect(info.trackers.length).toBe(2); // FB + GA
      expect(info.iframes.length).toBe(1);
      expect(info.thirdPartyScripts.length).toBe(1);
      expect(info.url).toBe('https://example.com/page');
      expect(info.domain).toBe('example.com');
    });

    test('should handle pages with no trackers', () => {
      document.body.innerHTML = '<div>Clean page</div>';

      const info = trackerService.getPageInfo();

      expect(info.trackers.length).toBe(0);
      expect(info.iframes.length).toBe(0);
      expect(info.thirdPartyScripts.length).toBe(0);
    });
  });

  describe('Summary Generation', () => {
    test('should generate accurate summary', () => {
      document.body.innerHTML = `
        <script>fbq('init', '111');</script>
        <script>fbq('init', '222');</script>
        <script>gtag('config', 'G-AAA');</script>
        <script>ga('create', 'UA-123-4', 'auto');</script>
        <iframe src="https://ads1.example.net/banner"></iframe>
        <iframe src="https://ads2.example.org/banner"></iframe>
      `;

      const script1 = document.createElement('script');
      script1.src = 'https://cdn1.example.net/script1.js';
      document.body.appendChild(script1);

      const script2 = document.createElement('script');
      script2.src = 'https://cdn2.example.org/script2.js';
      document.body.appendChild(script2);

      const summary = trackerService.getSummary();

      expect(summary.fbPixelCount).toBe(2);
      expect(summary.gaTrackerCount).toBe(2);
      expect(summary.iframeCount).toBe(2);
      expect(summary.thirdPartyScriptCount).toBe(2);
    });

    test('should return zeros for clean page', () => {
      document.body.innerHTML = '<div>No trackers</div>';

      const summary = trackerService.getSummary();

      expect(summary.fbPixelCount).toBe(0);
      expect(summary.gaTrackerCount).toBe(0);
      expect(summary.iframeCount).toBe(0);
      expect(summary.thirdPartyScriptCount).toBe(0);
    });
  });

  describe('Deduplication', () => {
    test('should deduplicate trackers by Type and Value', () => {
      const trackers = [
        { Type: 'fbPixel', Value: '123', Source: 'script' },
        { Type: 'fbPixel', Value: '123', Source: 'noscript' },
        { Type: 'ga4', Value: 'G-ABC', Source: 'script' },
        { Type: 'ga4', Value: 'G-ABC', Source: 'src' }
      ];

      const deduplicated = trackerService.deduplicateTrackers(trackers);

      expect(deduplicated.length).toBe(2);
      expect(deduplicated[0].Value).toBe('123');
      expect(deduplicated[1].Value).toBe('G-ABC');
    });

    test('should preserve different types with same value', () => {
      const trackers = [
        { Type: 'fbPixel', Value: '123', Source: 'script' },
        { Type: 'ga4', Value: '123', Source: 'script' }
      ];

      const deduplicated = trackerService.deduplicateTrackers(trackers);

      expect(deduplicated.length).toBe(2);
    });
  });
});
