// ============================================
// AntiScam Extension - Tracker Service
// Detects Facebook Pixels, Google Analytics, iFrames
// ============================================

class TrackerService {
  // Find all Facebook Pixels on page
  findFacebookPixels() {
    const pixels = [];

    // Check scripts
    const scripts = document.querySelectorAll('script');
    scripts.forEach(script => {
      const content = script.textContent || '';

      // Match fbq('init', 'PIXEL_ID')
      const matches = content.matchAll(/fbq\s*\(\s*['"]init['"]\s*,\s*['"](\d+)['"]/g);
      for (const match of matches) {
        pixels.push({
          Type: 'fbPixel',
          Value: match[1],
          Source: 'script'
        });
      }
    });

    // Check noscript tags
    const noscripts = document.querySelectorAll('noscript');
    noscripts.forEach(ns => {
      const content = ns.textContent || '';
      const matches = content.matchAll(/facebook\.com\/tr\?id=(\d+)/g);
      for (const match of matches) {
        pixels.push({
          Type: 'fbPixel',
          Value: match[1],
          Source: 'noscript'
        });
      }
    });

    // Deduplicate
    return this.deduplicateTrackers(pixels);
  }

  // Find all Google Analytics trackers
  findGoogleAnalytics() {
    const trackers = [];

    const scripts = document.querySelectorAll('script');
    scripts.forEach(script => {
      const content = script.textContent || '';
      const src = script.src || '';

      // GA4 (G-XXXXXXXX)
      const ga4Matches = content.matchAll(/['"]G-([A-Z0-9]+)['"]/g);
      for (const match of ga4Matches) {
        trackers.push({
          Type: 'ga4',
          Value: 'G-' + match[1],
          Source: 'script'
        });
      }

      // Check src for gtag
      if (src.includes('gtag/js?id=G-')) {
        const idMatch = src.match(/id=(G-[A-Z0-9]+)/);
        if (idMatch) {
          trackers.push({
            Type: 'ga4',
            Value: idMatch[1],
            Source: 'src'
          });
        }
      }

      // Universal Analytics (UA-XXXXX-X)
      const uaMatches = content.matchAll(/['"]UA-(\d+-\d+)['"]/g);
      for (const match of uaMatches) {
        trackers.push({
          Type: 'ua',
          Value: 'UA-' + match[1],
          Source: 'script'
        });
      }
    });

    return this.deduplicateTrackers(trackers);
  }

  // Find all external iFrames
  findExternalIframes() {
    const iframeDomains = [];
    const currentDomain = window.location.hostname;

    const iframes = document.querySelectorAll('iframe');
    iframes.forEach(iframe => {
      const src = iframe.src;
      if (src) {
        try {
          const url = new URL(src);
          const domain = url.hostname;

          if (domain && domain !== currentDomain && !iframeDomains.includes(domain)) {
            iframeDomains.push(domain);
          }
        } catch (e) {
          // Invalid URL
        }
      }
    });

    return iframeDomains;
  }

  // Find third-party scripts
  findThirdPartyScripts() {
    const scripts = [];
    const currentDomain = window.location.hostname;

    document.querySelectorAll('script[src]').forEach(script => {
      try {
        const url = new URL(script.src);
        if (url.hostname !== currentDomain) {
          scripts.push({
            domain: url.hostname,
            src: script.src
          });
        }
      } catch (e) {
        // Invalid URL
      }
    });

    return scripts;
  }

  // Get all page trackers and info
  getPageInfo() {
    const fbPixels = this.findFacebookPixels();
    const gaTrackers = this.findGoogleAnalytics();
    const iframes = this.findExternalIframes();
    const thirdPartyScripts = this.findThirdPartyScripts();

    return {
      trackers: [...fbPixels, ...gaTrackers],
      iframes: iframes,
      thirdPartyScripts: thirdPartyScripts,
      url: window.location.href,
      title: document.title,
      domain: window.location.hostname
    };
  }

  // Deduplicate trackers by Type+Value
  deduplicateTrackers(trackers) {
    const seen = new Set();
    return trackers.filter(tracker => {
      const key = `${tracker.Type}:${tracker.Value}`;
      if (seen.has(key)) {
        return false;
      }
      seen.add(key);
      return true;
    });
  }

  // Get summary
  getSummary() {
    const info = this.getPageInfo();
    return {
      fbPixelCount: info.trackers.filter(t => t.Type === 'fbPixel').length,
      gaTrackerCount: info.trackers.filter(t => t.Type === 'ga4' || t.Type === 'ua').length,
      iframeCount: info.iframes.length,
      thirdPartyScriptCount: info.thirdPartyScripts.length
    };
  }
}

// Singleton instance
export const trackerService = new TrackerService();
export default trackerService;
