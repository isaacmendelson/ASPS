# AntiScam Desktop Agent

Windows system tray application that provides real-time protection against online scams by integrating with the Chrome extension and backend server.

## Features

- System tray application with status indicators
- WebSocket communication with Chrome extension
- WebSocket connection to backend server
- Remote access software monitoring (AnyDesk, TeamViewer, etc.)
- Browser history analysis
- Local caching with TTL support
- JSON event logging

## Installation

### Prerequisites

- Python 3.8+
- Windows 10/11

### Setup

```bash
pip install -r requirements.txt
```

### Running

```bash
cd src
python main.py
```

### Building Executable

```bash
cd src
python build.py
```

Output: `dist/AntiScam.exe`

## Configuration

Edit `src/config.py`:

```python
BACKEND_URL = "wss://your-server.com/ws"
EXTENSION_PORTS = [8080, 8181, 8282, 8383, 8484]
```

## Project Structure

```
desktop-agent-windows/
├── src/
│   ├── main.py              # Main application entry point
│   ├── config.py            # Configuration settings
│   ├── models.py            # Message models
│   ├── remote_monitor.py    # Remote access software detection
│   ├── browser_history.py   # Browser history reader
│   ├── cache_manager.py     # Local cache management
│   ├── event_logger.py      # Event logging
│   ├── extension_server.py  # WebSocket server for extension
│   ├── signalr_client.py    # Backend SignalR client
│   ├── tray_icon.py         # System tray interface
│   └── build.py             # Build script
├── requirements.txt
└── README.md
```

## Communication Protocol

### Extension to Agent (WebSocket localhost)

```json
{
  "type": "url_check",
  "url": "https://example.com",
  "trackers": [{"Type": "fbPixel", "Value": "123"}],
  "iframes": ["ads.example.com"]
}
```

### Agent to Backend (WebSocket)

See `models.py` for complete protocol specification.

## Local Storage

Data stored in `~/.antiscam/`:

- `cache.json` - URL check cache
- `events.jsonl` - Event log
- `config.json` - Local configuration (includes device_id)

## License

Proprietary
