# AntiScam Desktop Agent - Installation Guide

## 📥 Installation Options

### Option 1: Windows Installer (Recommended)

**For End Users**

1. **Download** `AntiScamDesktop-Setup-0.0.0.3.exe`
2. **Run** the installer
3. Follow the installation wizard
4. Choose installation options:
   - ✅ Create desktop shortcut (optional)
   - ✅ Start with Windows (recommended)
5. Click "Install"
6. Launch AntiScam from desktop or Start Menu

**System Requirements:**
- Windows 10 or Windows 11
- 100 MB free disk space
- Internet connection

---

### Option 2: Standalone Executable

**For Advanced Users or Portable Use**

1. **Download** `AntiScam.exe` from the releases page
2. **Create** a folder (e.g., `C:\Program Files\AntiScam\`)
3. **Copy** `AntiScam.exe` to the folder
4. **Run** `AntiScam.exe`
5. (Optional) Right-click → "Create shortcut" → Move to Desktop

**To run on startup:**
1. Press `Win+R` → type `shell:startup`
2. Create a shortcut to `AntiScam.exe` in the Startup folder

---

## 🔧 First Launch Configuration

### Automatic Setup
On first launch, AntiScam will:
1. Create configuration directory: `%USERPROFILE%\.antiscam\`
2. Generate unique device ID
3. Connect to the backend server (if configured)
4. Show system tray icon

### Manual Configuration (Optional)

Edit `%USERPROFILE%\.antiscam\config.json`:

```json
{
  "device_id": "auto-generated-uuid",
  "backend_url": "wss://your-server.com/ws",
  "extension_ports": [8080, 8181, 8282, 8383, 8484],
  "auto_start": true,
  "notifications_enabled": true
}
```

---

## 🛡️ Features

### Real-Time Protection
- **URL Analysis:** Browser extension sends URLs for risk assessment
- **Remote Access Monitoring:** Detects AnyDesk, TeamViewer, RustDesk, etc.
- **Windows Toast Notifications:** Risk-based alerts with sound

### System Tray
- **Green:** Protected, no threats
- **Yellow:** Warning or suspicious activity
- **Red:** Active threat detected

**Right-click menu:**
- Show Status
- Enable/Disable Protection
- Settings
- Exit

---

## 📝 Browser Extension Setup

1. **Install** AntiScam Chrome Extension from the Chrome Web Store
2. **Desktop Agent** will automatically detect the extension
3. **Connection status** shown in system tray tooltip

---

## 🔍 Troubleshooting

### Agent not starting
- Check Windows Event Viewer for error logs
- Verify `%USERPROFILE%\.antiscam\` directory exists
- Run as Administrator (if needed)

### No connection to extension
- Verify extension is installed and enabled
- Check firewall settings (allow localhost ports 8080-8484)
- Restart both agent and browser

### Notifications not showing
- Verify Windows notifications are enabled (Settings → Notifications)
- Check Focus Assist is not blocking notifications

### Backend connection failed
- Verify internet connection
- Check `backend_url` in config.json
- Check server logs

---

## 📂 File Locations

| Item | Location |
|------|----------|
| **Executable** | `C:\Program Files\AntiScam\AntiScam.exe` |
| **Config** | `%USERPROFILE%\.antiscam\config.json` |
| **Cache** | `%USERPROFILE%\.antiscam\cache.json` |
| **Logs** | `%USERPROFILE%\.antiscam\events.jsonl` |

---

## 🔄 Updating

### Via Installer
1. Download new installer
2. Run installer (will upgrade existing installation)
3. Restart AntiScam

### Manual Update
1. Stop AntiScam (right-click tray icon → Exit)
2. Replace `AntiScam.exe` with new version
3. Start AntiScam

---

## 🗑️ Uninstallation

### Via Windows Settings
1. Open **Settings** → **Apps**
2. Find **AntiScam Desktop Agent**
3. Click **Uninstall**

### Manual Removal
1. Stop AntiScam (right-click tray icon → Exit)
2. Delete installation folder
3. Delete `%USERPROFILE%\.antiscam\` (to remove settings)
4. Remove startup shortcut (if created manually)

---

## 🆘 Support

**Issues?** Contact support:
- Email: support@antiscam.io
- GitHub: https://github.com/your-org/asps/issues

**Logs for support:**
1. Collect `%USERPROFILE%\.antiscam\events.jsonl`
2. Include Windows version and error message
