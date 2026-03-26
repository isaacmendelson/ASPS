# AntiScam Desktop Agent

Windows system tray application that provides real-time protection against online scams by integrating with the Chrome extension and backend server.

## 🎯 Features

- **System Tray Application** with status indicators
- **Windows Toast Notifications** with risk-based alerts
- **WebSocket Communication** with Chrome extension
- **Backend Integration** via WebSocket/ZeroMQ
- **Remote Access Monitoring** (AnyDesk, TeamViewer, RustDesk, etc.)
- **Browser History Analysis**
- **Local Caching** with TTL support
- **JSON Event Logging**

## 📥 Installation

### For End Users

See **[INSTALL.md](INSTALL.md)** for detailed installation instructions.

**Quick Install:**
1. Download `AntiScamDesktop-Setup-x.x.x.exe`
2. Run installer and follow wizard
3. Launch from Start Menu or Desktop

### For Developers

**Prerequisites:**
- Python 3.8+
- Windows 10/11

**Setup:**
```bash
# Install dependencies
pip install -r requirements.txt

# Run from source
cd src
python main.py
```

## 🔨 Building from Source

### Build Standalone EXE

```bash
cd src
python build.py
```

Output: `dist/AntiScam.exe`

### Build Complete Release (EXE + Installer + ZIP)

```bash
python build_release.py
```

**Output in `release/`:**
- `AntiScam.exe` - Standalone executable
- `AntiScamDesktop-Setup-x.x.x.exe` - Windows installer (requires Inno Setup)
- `AntiScamDesktop-vx.x.x-Standalone.zip` - Portable package
- `checksums.txt` - SHA256 checksums

**Requirements for full build:**
- [PyInstaller](https://pyinstaller.org/) (included in requirements.txt)
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) (optional, for installer)

## ⚙️ Configuration

### Auto Configuration
On first launch, the agent creates `%USERPROFILE%\.antiscam\config.json`

### Manual Configuration
Edit `config.json`:

```json
{
  "device_id": "auto-generated-uuid",
  "backend_url": "wss://your-server.com/ws",
  "extension_ports": [8080, 8181, 8282, 8383, 8484],
  "auto_start": true,
  "notifications_enabled": true
}
```

Or edit `src/config.py` before building:

```python
BACKEND_URL = "wss://your-server.com/ws"
EXTENSION_PORTS = [8080, 8181, 8282, 8383, 8484]
```

## 📁 Project Structure

```
apps/desktop/win/
├── src/
│   ├── main.py                    # Main application entry
│   ├── config.py                  # Configuration
│   ├── models.py                  # Message models
│   ├── tray_icon.py               # System tray UI
│   ├── notification_manager.py    # Windows Toast notifications
│   ├── remote_monitor.py          # Remote access detection
│   ├── browser_history.py         # Browser history reader
│   ├── cache_manager.py           # Local cache
│   ├── event_logger.py            # Event logging
│   ├── extension_server.py        # WebSocket server for extension
│   ├── zmq_client.py              # Backend ZeroMQ client
│   ├── build.py                   # Simple EXE builder
│   ├── version.py                 # Version info
│   ├── services/                  # Business logic
│   │   ├── monitor_service.py     # Monitoring orchestration
│   │   ├── scan_service.py        # URL scanning
│   │   └── protection_service.py  # Protection features
│   ├── handlers/                  # Request handlers
│   │   ├── extension_handler.py   # Extension requests
│   │   └── notification_handler.py # Notification logic
│   └── tests/                     # Unit tests
│       ├── test_notification_manager.py
│       └── test_remote_monitor.py
├── build_release.py               # Release build script
├── installer.iss                  # Inno Setup script
├── requirements.txt               # Python dependencies
├── README.md                      # This file
├── INSTALL.md                     # Installation guide
└── LICENSE.txt                    # License

Output directories (created on build):
├── dist/                          # Built executables
├── build/                         # PyInstaller temp files
├── release/                       # Final release artifacts
└── installer_output/              # Inno Setup output
```

## 🔌 Communication Protocol

### Extension → Agent (WebSocket localhost)

```json
{
  "type": "url_check",
  "url": "https://example.com",
  "trackers": [{"Type": "fbPixel", "Value": "123"}],
  "iframes": ["ads.example.com"]
}
```

### Agent → Backend (WebSocket/ZeroMQ)

See `src/models.py` for complete protocol specification.

## 💾 Local Storage

Data stored in `%USERPROFILE%\.antiscam\`:

| File | Description |
|------|-------------|
| `config.json` | Local configuration & device ID |
| `cache.json` | URL check cache (TTL-based) |
| `events.jsonl` | Event log (JSONL format) |

## 🧪 Testing

```bash
# Run all tests
cd src
python -m unittest discover -s tests -v

# Run specific test
python -m unittest tests.test_notification_manager -v

# Run with pytest (if installed)
pytest tests/ -v
```

## 🚀 Development Workflow

1. **Make changes** to `src/`
2. **Run tests**: `python -m unittest discover -s tests`
3. **Test locally**: `python src/main.py`
4. **Build EXE**: `python src/build.py`
5. **Test EXE**: Run `dist/AntiScam.exe`
6. **Create release**: `python build_release.py`

## 🐛 Troubleshooting

See **[INSTALL.md](INSTALL.md)** - Troubleshooting section

**Common Issues:**
- **Import errors**: Make sure all dependencies are installed
- **Toast not showing**: Install `winotify` or `win10toast`
- **WebSocket errors**: Check firewall settings

## 📊 JIRA Tasks

- **ASPS-381**: Windows Toast Notifications ✅ (ready-for-qa)
- **ASPS-16**: Agent Executable & Installer 🔨 (in progress)
- **ASPS-46**: Create Tests for Agent 📝 (planned)

## 📄 License

Proprietary - See [LICENSE.txt](LICENSE.txt)

## 👥 Team

- **Yuri** (Python Developer) - Agent implementation
- **Alex** (CTO) - Architecture & backend integration
- **Zappa** (CEO) - Product direction
- **Isaac** - Technical oversight
