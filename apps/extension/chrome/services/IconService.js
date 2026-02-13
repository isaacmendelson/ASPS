// ============================================
// AntiScam Extension - Icon Service
// Manages extension icon appearance
// ============================================

import stateManager from '../state/StateManager.js';

class IconService {
  constructor() {
    this.colors = {
      green: '#00C853',
      yellow: '#FFD600',
      red: '#FF1744',
      gray: '#9E9E9E'
    };

    this.currentColor = null;

    // Loading animation state
    this.animationInterval = null;
    this.frame = 0;
    this.loadingBadgeTimeout = null;
    this.isLoading = false;

    // Subscribe to state changes
    stateManager.subscribe('connection.desktop', (connected) => {
      if (connected) {
        this.setColor('green');
      } else {
        this.setColor('gray');
      }
    });

    stateManager.subscribe('scan.score', (score) => {
      if (score !== null && stateManager.get('connection.desktop')) {
        this.setColorByScore(score);
      }
    });
  }

  // Start loading animation with pulsing gray icon
  startLoadingAnimation() {
    this.isLoading = true;

    // Don't restart if already animating
    if (this.animationInterval) return;

    // Reset frame counter
    this.frame = 0;

    // Start animation at 20 FPS (50ms interval)
    this.animationInterval = setInterval(() => {
      this.drawLoadingFrame();
    }, 50);

    // Show badge loading symbol after 500ms delay (avoids flicker on fast responses)
    this.loadingBadgeTimeout = setTimeout(async () => {
      if (this.isLoading) {
        await chrome.action.setBadgeText({ text: '\u21BB' }); // Clockwise arrow
        await chrome.action.setBadgeBackgroundColor({ color: '#9E9E9E' });
      }
    }, 500);

    console.log('[IconService] Loading animation started');
  }

  // Stop loading animation
  stopLoadingAnimation() {
    this.isLoading = false;

    // Clear animation interval
    if (this.animationInterval) {
      clearInterval(this.animationInterval);
      this.animationInterval = null;
    }

    // Clear badge timeout
    if (this.loadingBadgeTimeout) {
      clearTimeout(this.loadingBadgeTimeout);
      this.loadingBadgeTimeout = null;
    }

    // Clear badge text (let caller set appropriate icon color after)
    chrome.action.setBadgeText({ text: '' });

    // Reset currentColor so next setColor will always redraw
    // (loading animation changes the icon, so we need to redraw even if same color)
    this.currentColor = null;

    console.log('[IconService] Loading animation stopped');
  }

  // Draw a single frame of the loading animation
  drawLoadingFrame() {
    try {
      const canvas = new OffscreenCanvas(128, 128);
      const ctx = canvas.getContext('2d');

      // Breathing opacity: oscillates between 0.4 and 1.0
      const breathingOpacity = 0.4 + 0.6 * (0.5 + 0.5 * Math.sin(this.frame * 0.15));

      ctx.clearRect(0, 0, 128, 128);
      ctx.globalAlpha = breathingOpacity;

      // Draw gray shield during loading
      this.drawIcon(ctx, '#9E9E9E', 'gray');

      const imageData = ctx.getImageData(0, 0, 128, 128);
      chrome.action.setIcon({ imageData: imageData });

      this.frame++;
    } catch (e) {
      console.error('[IconService] Error drawing loading frame:', e);
    }
  }

  // Set icon color
  setColor(color) {
    // Stop any loading animation before setting a new color
    this.stopLoadingAnimation();

    if (this.currentColor === color) return;

    this.currentColor = color;
    const fillColor = this.colors[color] || this.colors.gray;

    try {
      const canvas = new OffscreenCanvas(128, 128);
      const ctx = canvas.getContext('2d');

      this.drawIcon(ctx, fillColor, color);

      const imageData = ctx.getImageData(0, 0, 128, 128);
      chrome.action.setIcon({ imageData: imageData });

      chrome.storage.local.set({ iconColor: color });

      console.log(`[IconService] Icon set to ${color}`);
    } catch (e) {
      console.error('[IconService] Error setting icon:', e);
    }
  }

  // Set icon color based on protective action from server (with score fallback)
  setColorByAction(protectiveAction, score = null) {
    // 0=None, 1=Notify, 2=WarnBanner, 3=WarnModal, 4=Block
    if (protectiveAction >= 4) {
      this.setColor('red');     // Block
    } else if (protectiveAction >= 2) {
      this.setColor('yellow');  // Warning
    } else {
      // Fallback to score-based display if protectiveAction is 0 or 1
      // This handles cases where server doesn't set protectiveAction correctly
      if (score !== null && score !== undefined) {
        if (score <= 30) {
          this.setColor('red');
          return;
        } else if (score <= 60) {
          this.setColor('yellow');
          return;
        }
      }
      this.setColor('green');   // Safe
    }
  }

  // Legacy: Set icon color based on score (for backward compatibility)
  setColorByScore(score) {
    // Keep for backward compatibility but prefer setColorByAction
    if (score <= 30) {
      this.setColor('red');
    } else if (score <= 60) {
      this.setColor('yellow');
    } else {
      this.setColor('green');
    }
  }

  // Draw the icon
  drawIcon(ctx, fillColor, colorName) {
    ctx.clearRect(0, 0, 128, 128);

    // Shield background
    ctx.beginPath();
    ctx.moveTo(64, 8);
    ctx.lineTo(112, 32);
    ctx.lineTo(112, 72);
    ctx.quadraticCurveTo(112, 112, 64, 120);
    ctx.quadraticCurveTo(16, 112, 16, 72);
    ctx.lineTo(16, 32);
    ctx.closePath();
    ctx.fillStyle = fillColor;
    ctx.fill();

    // Inner shield (white)
    ctx.beginPath();
    ctx.moveTo(64, 20);
    ctx.lineTo(100, 40);
    ctx.lineTo(100, 70);
    ctx.quadraticCurveTo(100, 100, 64, 108);
    ctx.quadraticCurveTo(28, 100, 28, 70);
    ctx.lineTo(28, 40);
    ctx.closePath();
    ctx.fillStyle = '#FFFFFF';
    ctx.fill();

    // Symbol based on color
    this.drawSymbol(ctx, fillColor, colorName);
  }

  // Draw symbol inside icon
  drawSymbol(ctx, fillColor, colorName) {
    ctx.beginPath();

    switch (colorName) {
      case 'green':
        // Checkmark
        ctx.moveTo(44, 64);
        ctx.lineTo(56, 80);
        ctx.lineTo(84, 48);
        ctx.lineWidth = 8;
        ctx.strokeStyle = fillColor;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        ctx.stroke();
        break;

      case 'red':
        // X mark
        ctx.moveTo(48, 48);
        ctx.lineTo(80, 80);
        ctx.moveTo(80, 48);
        ctx.lineTo(48, 80);
        ctx.lineWidth = 8;
        ctx.strokeStyle = fillColor;
        ctx.lineCap = 'round';
        ctx.stroke();
        break;

      case 'yellow':
        // Warning !
        ctx.font = 'bold 48px Arial';
        ctx.fillStyle = fillColor;
        ctx.textAlign = 'center';
        ctx.fillText('!', 64, 78);
        break;

      case 'gray':
      default:
        // Question mark
        ctx.font = 'bold 40px Arial';
        ctx.fillStyle = fillColor;
        ctx.textAlign = 'center';
        ctx.fillText('?', 64, 76);
        break;
    }
  }

  // Update icon based on current state
  update() {
    const connected = stateManager.get('connection.desktop');
    const score = stateManager.get('scan.score');

    if (!connected) {
      this.setColor('gray');
    } else if (score !== null) {
      this.setColorByScore(score);
    } else {
      this.setColor('green');
    }
  }

  // Get current color
  getColor() {
    return this.currentColor;
  }
}

// Singleton instance
export const iconService = new IconService();
export default iconService;
