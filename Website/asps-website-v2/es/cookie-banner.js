/**
 * ASPS Cookie Consent Banner
 * Legal compliance: CCPA/CPRA (California), Israeli Privacy Protection Law
 * 
 * Features:
 * - Accept All / Essential Only buttons
 * - Stores choice in localStorage (both accept and decline)
 * - Pre-consent: Only essential functionality runs until user decides
 * - Privacy Policy link
 * - Responsive design matching ASPS brand
 */
(function(){
  'use strict';

  // Check if user has already made a choice
  var consentStatus = localStorage.getItem('asps_cookie_consent');
  if(consentStatus) return; // User already decided (accepted or declined)

  // Inject styles
  var css = `
    #asps-cookie-banner {
      position: fixed;
      bottom: 0;
      left: 0;
      right: 0;
      z-index: 9999;
      background: rgba(10, 14, 32, 0.98);
      backdrop-filter: blur(16px);
      -webkit-backdrop-filter: blur(16px);
      border-top: 1px solid rgba(255,255,255,0.1);
      padding: 20px 60px;
      display: flex;
      align-items: center;
      gap: 24px;
      font-family: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
      animation: cookieSlideUp 0.5s cubic-bezier(0.16, 1, 0.3, 1);
      box-shadow: 0 -4px 30px rgba(0,0,0,0.3);
    }
    
    @keyframes cookieSlideUp {
      from { 
        transform: translateY(100%); 
        opacity: 0; 
      }
      to { 
        transform: translateY(0); 
        opacity: 1; 
      }
    }
    
    #asps-cookie-banner .cookie-content {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    
    #asps-cookie-banner .cookie-title {
      font-size: 0.92rem;
      font-weight: 600;
      color: #FFFFFF;
      margin: 0;
      display: flex;
      align-items: center;
      gap: 8px;
    }
    
    #asps-cookie-banner .cookie-icon {
      font-size: 1.1rem;
    }
    
    #asps-cookie-banner .cookie-text {
      margin: 0;
      font-size: 0.82rem;
      color: rgba(255,255,255,0.68);
      line-height: 1.6;
      max-width: 640px;
    }
    
    #asps-cookie-banner a {
      color: #22C55E;
      text-decoration: none;
      font-weight: 500;
      transition: color 0.2s ease;
    }
    
    #asps-cookie-banner a:hover {
      color: #4ADE80;
      text-decoration: underline;
    }
    
    #asps-cookie-banner .cookie-buttons {
      display: flex;
      gap: 12px;
      flex-shrink: 0;
    }
    
    .cookie-btn {
      padding: 11px 22px;
      border-radius: 8px;
      font-family: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
      font-size: 0.84rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s ease;
      border: none;
      white-space: nowrap;
    }
    
    .cookie-btn-accept {
      background: #22C55E;
      color: #FFFFFF;
    }
    
    .cookie-btn-accept:hover {
      background: #16A34A;
      transform: translateY(-1px);
      box-shadow: 0 4px 12px rgba(34, 197, 94, 0.35);
    }
    
    .cookie-btn-accept:focus-visible {
      outline: 3px solid #22C55E;
      outline-offset: 3px;
    }
    
    .cookie-btn-essential {
      background: transparent;
      color: rgba(255,255,255,0.85);
      border: 1.5px solid rgba(255,255,255,0.25);
    }
    
    .cookie-btn-essential:hover {
      border-color: rgba(255,255,255,0.5);
      color: #FFFFFF;
      background: rgba(255,255,255,0.05);
    }
    
    .cookie-btn-essential:focus-visible {
      outline: 3px solid #22C55E;
      outline-offset: 3px;
    }
    
    /* Responsive: Tablet */
    @media (max-width: 900px) {
      #asps-cookie-banner {
        padding: 18px 32px;
        gap: 20px;
      }
    }
    
    /* Responsive: Mobile */
    @media (max-width: 768px) {
      #asps-cookie-banner {
        flex-direction: column;
        align-items: stretch;
        padding: 20px 20px 24px;
        gap: 16px;
      }
      
      #asps-cookie-banner .cookie-content {
        text-align: center;
      }
      
      #asps-cookie-banner .cookie-title {
        justify-content: center;
      }
      
      #asps-cookie-banner .cookie-text {
        max-width: 100%;
      }
      
      #asps-cookie-banner .cookie-buttons {
        flex-direction: column;
        gap: 10px;
      }
      
      .cookie-btn {
        width: 100%;
        padding: 13px 20px;
        text-align: center;
      }
    }
    
    /* Reduced motion preference */
    @media (prefers-reduced-motion: reduce) {
      #asps-cookie-banner {
        animation: none;
      }
      .cookie-btn {
        transition: none;
      }
    }
  `;

  // Inject CSS
  var style = document.createElement('style');
  style.textContent = css;
  document.head.appendChild(style);

  // Create banner
  var banner = document.createElement('div');
  banner.id = 'asps-cookie-banner';
  banner.setAttribute('role', 'dialog');
  banner.setAttribute('aria-modal', 'false');
  banner.setAttribute('aria-label', 'Cookie consent');
  banner.setAttribute('aria-describedby', 'cookie-desc');

  banner.innerHTML = `
    <div class="cookie-content">
      <p class="cookie-title">
        <span class="cookie-icon" aria-hidden="true">🍪</span>
        Your Privacy Matters
      </p>
      <p class="cookie-text" id="cookie-desc">
        We use cookies and similar technologies to operate ASPS, understand how visitors use our site, and improve your experience. 
        You can accept all cookies or choose only the essential ones needed for the site to function.
        <a href="privacy.html">Privacy Policy</a>
      </p>
    </div>
    <div class="cookie-buttons">
      <button type="button" class="cookie-btn cookie-btn-accept" id="cookie-accept-all">
        Accept All
      </button>
      <button type="button" class="cookie-btn cookie-btn-essential" id="cookie-essential-only">
        Essential Only
      </button>
    </div>
  `;

  document.body.appendChild(banner);

  // Dismiss function with animation
  function dismiss(status) {
    localStorage.setItem('asps_cookie_consent', status);
    localStorage.setItem('asps_cookie_consent_date', new Date().toISOString());
    
    banner.style.transition = 'transform 0.35s ease, opacity 0.35s ease';
    banner.style.transform = 'translateY(100%)';
    banner.style.opacity = '0';
    
    setTimeout(function() {
      banner.remove();
      
      // If user accepted all, fire custom event for any analytics that might be added later
      if (status === 'accepted_all') {
        window.dispatchEvent(new CustomEvent('asps-consent-granted', { 
          detail: { type: 'all' } 
        }));
      } else if (status === 'essential_only') {
        window.dispatchEvent(new CustomEvent('asps-consent-granted', { 
          detail: { type: 'essential' } 
        }));
      }
    }, 350);
  }

  // Event listeners
  document.getElementById('cookie-accept-all').addEventListener('click', function() {
    dismiss('accepted_all');
  });

  document.getElementById('cookie-essential-only').addEventListener('click', function() {
    dismiss('essential_only');
  });

  // Trap focus within banner for accessibility
  banner.addEventListener('keydown', function(e) {
    if (e.key === 'Escape') {
      // On Escape, default to essential only
      dismiss('essential_only');
    }
  });

})();

/**
 * Helper function for checking consent status
 * Can be used by other scripts to check if tracking is allowed
 * 
 * Usage:
 *   if (window.aspsConsentAllowed && window.aspsConsentAllowed('analytics')) {
 *     // Load analytics
 *   }
 */
window.aspsConsentAllowed = function(type) {
  var consent = localStorage.getItem('asps_cookie_consent');
  
  if (type === 'essential') {
    // Essential cookies are always allowed
    return true;
  }
  
  if (type === 'analytics' || type === 'marketing' || type === 'all') {
    // Non-essential requires full consent
    return consent === 'accepted_all';
  }
  
  return false;
};

/**
 * Helper to get consent status
 * Returns: 'accepted_all', 'essential_only', or null (not decided)
 */
window.aspsGetConsentStatus = function() {
  return localStorage.getItem('asps_cookie_consent');
};

/**
 * Helper to reset consent (for privacy policy page "change preferences" link)
 */
window.aspsResetConsent = function() {
  localStorage.removeItem('asps_cookie_consent');
  localStorage.removeItem('asps_cookie_consent_date');
  location.reload();
};
