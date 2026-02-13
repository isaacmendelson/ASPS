# AntiScam Chrome Extension

Chrome browser extension that provides real-time protection against online scams, phishing, and fraudulent websites.

## Features

- Real-time URL scanning and risk assessment
- Integration with desktop agent for enhanced protection
- Google OAuth authentication
- Visual warning overlays for suspicious sites
- Tracker and iframe detection
- Local caching for performance optimization

## Installation

### Development

1. Open Chrome and navigate to `chrome://extensions/`
2. Enable "Developer mode"
3. Click "Load unpacked" and select this directory

### Production

Build and publish to Chrome Web Store.

## Project Structure

```
extension-chrome/
├── manifest.json           # Extension manifest (MV3)
├── background.js           # Service worker
├── content.js              # Content script
├── content.css             # Content styles
├── popup.html              # Popup UI
├── popup.js                # Popup logic
├── popup.css               # Popup styles
├── icons/                  # Extension icons
├── messaging/              # Message bus system
│   ├── MessageBus.js
│   ├── MessageTypes.js
│   └── index.js
├── services/               # Core services
│   ├── AuthService.js      # Google OAuth handling
│   ├── CacheService.js     # Local caching
│   ├── ConnectionService.js# Desktop agent connection
│   ├── IconService.js      # Badge/icon management
│   ├── ProtectionService.js# Protection state management
│   ├── ScanService.js      # URL scanning
│   ├── TrackerService.js   # Tracker detection
│   └── index.js
├── state/                  # State management
│   ├── StateManager.js
│   └── index.js
├── utils/                  # Utilities
│   └── helpers.js
└── warning/                # Warning overlay system
    ├── FrictionController.js
    ├── RemoteAccessWarning.js
    ├── ShadowContainer.js
    ├── warning-styles.js
    └── index.js
```

## Permissions

- `activeTab` - Access current tab
- `tabs` - Tab management
- `webNavigation` - Navigation events
- `storage` - Local storage
- `identity` - Google OAuth
- `notifications` - User notifications
- `alarms` - Scheduled tasks

## Communication

### With Desktop Agent

Connects via WebSocket to localhost on configured ports (8080-8484).

### Message Format

```json
{
  "type": "scan_request",
  "url": "https://example.com",
  "trackers": [],
  "iframes": []
}
```

## License

Proprietary
