"""
Build Release Script for AntiScam Desktop Agent
Creates:
1. Standalone EXE (via PyInstaller)
2. Windows Installer (via Inno Setup - if available)
3. Release archive (ZIP)

Environment overrides:
    python build_release.py                   # default config (local testing)
    python build_release.py --env dev         # uses src/config_dev.py
    python build_release.py --env production  # uses src/config_production.py
    ...

When `--env <name>` is given, src/config_<name>.py is copied to
src/config_override.py before PyInstaller runs. `config.py` imports
config_override at module load, replacing any default value (BACKEND_HOST,
WEBAPI_URL, ...) with the env-specific one. After the build the override
file is removed so the source tree stays clean.
"""

import argparse
import os
import sys
import shutil
import subprocess
import zipfile
from pathlib import Path
from datetime import datetime

# ─── Argument parsing ───────────────────────────────────────────────────────
_parser = argparse.ArgumentParser(description="Build AntiScam Desktop Agent")
_parser.add_argument(
    "--env", "--environment",
    dest="env",
    default=None,
    help="Apply src/config_<env>.py overrides (e.g. dev, production, aws)",
)
_args = _parser.parse_args()
ENV_NAME = _args.env

# Paths
ROOT_DIR = Path(__file__).parent.absolute()
SRC_DIR = ROOT_DIR / "src"
DIST_DIR = ROOT_DIR / "dist"
BUILD_DIR = ROOT_DIR / "build"
RELEASE_DIR = ROOT_DIR / "release"
INSTALLER_OUTPUT = ROOT_DIR / "installer_output"

OVERRIDE_FILE = SRC_DIR / "config_override.py"

# Version (from src/version.py)
sys.path.insert(0, str(SRC_DIR))
from version import __version__

# ─── Environment override file: copy config_<env>.py → config_override.py ───
def _cleanup_override():
    """Remove config_override.py from source tree on script exit."""
    if OVERRIDE_FILE.exists():
        try:
            OVERRIDE_FILE.unlink()
        except Exception:
            pass

import atexit
atexit.register(_cleanup_override)

if ENV_NAME:
    env_source = SRC_DIR / f"config_{ENV_NAME}.py"
    if not env_source.exists():
        available = sorted(p.stem.replace("config_", "")
                           for p in SRC_DIR.glob("config_*.py")
                           if p.stem != "config_override")
        print(f"❌ Environment '{ENV_NAME}' not found. Looked for: {env_source}")
        if available:
            print(f"   Available: {', '.join(available)}")
        sys.exit(1)
    shutil.copy2(env_source, OVERRIDE_FILE)
    print(f"📋 Environment override: {env_source.name} → config_override.py")

VERSION_TAG = f"{__version__}-{ENV_NAME}" if ENV_NAME else __version__

print("=" * 70)
print(f"AntiScam Desktop Agent - Release Builder v{VERSION_TAG}")
print("=" * 70)
print()

# Clean previous builds
# NOTE: RELEASE_DIR is NOT cleaned — we want successive `--env dev` and
# `--env prod` builds to accumulate ZIPs in the same release directory.
print("🧹 Cleaning previous builds...")
for dir_path in [DIST_DIR, BUILD_DIR, INSTALLER_OUTPUT]:
    if dir_path.exists():
        shutil.rmtree(dir_path)
        print(f"   Removed: {dir_path}")

# Create release directory
RELEASE_DIR.mkdir(exist_ok=True)
print()

# Step 1: Build EXE with PyInstaller
print("🔨 Step 1: Building standalone EXE...")
print("-" * 70)

try:
    import PyInstaller.__main__
    
    pyinstaller_args = [
        str(SRC_DIR / 'main.py'),
        '--name=AntiScam',
        '--onefile',
        '--windowed',
        '--clean',
        '--noconfirm',
        f'--distpath={DIST_DIR}',
        f'--workpath={BUILD_DIR}',
        # Hidden imports PyInstaller's static analysis might miss
        '--hidden-import=pystray._win32',
        '--hidden-import=PIL._tkinter_finder',
        '--hidden-import=keyring.backends.Windows',
        '--hidden-import=keyring.backends.fail',
        # customtkinter ships theme JSONs and fonts that must be bundled
        '--collect-all=customtkinter',
        # Optional toast libraries (only collect if installed)
        '--collect-submodules=winotify',
        # Collect data files (icons etc.)
        f'--add-data={SRC_DIR}/data;data',
        # Icon (if exists)
        # '--icon=icon.ico',
    ]
    
    PyInstaller.__main__.run(pyinstaller_args)
    
    exe_path = DIST_DIR / "AntiScam.exe"
    if not exe_path.exists():
        raise FileNotFoundError("EXE was not created!")
    
    exe_size_mb = exe_path.stat().st_size / (1024 * 1024)
    print(f"✅ EXE built successfully: {exe_path}")
    print(f"   Size: {exe_size_mb:.2f} MB")
    print()
    
except Exception as e:
    print(f"❌ Failed to build EXE: {e}")
    sys.exit(1)

# Step 2: Create ZIP archive
print("📦 Step 2: Creating ZIP archive...")
print("-" * 70)

zip_filename = f"AntiScamDesktop-v{VERSION_TAG}-Standalone.zip"
zip_path = RELEASE_DIR / zip_filename

try:
    with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as zipf:
        zipf.write(exe_path, "AntiScam.exe")
        zipf.write(ROOT_DIR / "README.md", "README.md")
        zipf.write(ROOT_DIR / "INSTALL.md", "INSTALL.md")
        zipf.write(ROOT_DIR / "LICENSE.txt", "LICENSE.txt")
    
    zip_size_mb = zip_path.stat().st_size / (1024 * 1024)
    print(f"✅ ZIP created: {zip_path}")
    print(f"   Size: {zip_size_mb:.2f} MB")
    print()
    
except Exception as e:
    print(f"❌ Failed to create ZIP: {e}")
    sys.exit(1)

# Step 3: Build Windows Installer (if Inno Setup is available)
print("🎁 Step 3: Building Windows Installer...")
print("-" * 70)

inno_setup_paths = [
    r"C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    r"C:\Program Files\Inno Setup 6\ISCC.exe",
]

iscc_exe = None
for path in inno_setup_paths:
    if os.path.exists(path):
        iscc_exe = path
        break

if iscc_exe:
    try:
        subprocess.run([
            iscc_exe,
            str(ROOT_DIR / "installer.iss")
        ], check=True)
        
        installer_file = list(INSTALLER_OUTPUT.glob("*.exe"))[0]
        final_installer = RELEASE_DIR / installer_file.name
        shutil.move(str(installer_file), str(final_installer))
        
        installer_size_mb = final_installer.stat().st_size / (1024 * 1024)
        print(f"✅ Installer created: {final_installer}")
        print(f"   Size: {installer_size_mb:.2f} MB")
        print()
        
    except Exception as e:
        print(f"⚠️  Failed to build installer: {e}")
        print("   Continuing without installer...")
        print()
else:
    print("⚠️  Inno Setup not found. Skipping installer creation.")
    print("   Download from: https://jrsoftware.org/isinfo.php")
    print()

# Step 4: Create checksums
print("🔐 Step 4: Creating checksums...")
print("-" * 70)

import hashlib

checksum_file = RELEASE_DIR / "checksums.txt"
with open(checksum_file, 'w') as f:
    f.write(f"AntiScam Desktop Agent v{__version__} - SHA256 Checksums\n")
    f.write(f"Generated: {datetime.now().isoformat()}\n")
    f.write("=" * 70 + "\n\n")
    
    for file in RELEASE_DIR.glob("*"):
        if file.is_file() and file != checksum_file:
            sha256 = hashlib.sha256()
            with open(file, 'rb') as rf:
                for chunk in iter(lambda: rf.read(4096), b''):
                    sha256.update(chunk)
            
            checksum = sha256.hexdigest()
            f.write(f"{checksum}  {file.name}\n")
            print(f"   {file.name}: {checksum[:16]}...")

print(f"\n✅ Checksums saved to: {checksum_file}")
print()

# Summary
print("=" * 70)
print("🎉 BUILD COMPLETE!")
print("=" * 70)
print(f"\nRelease artifacts in: {RELEASE_DIR}\n")
print("Files created:")
for file in sorted(RELEASE_DIR.glob("*")):
    size_mb = file.stat().st_size / (1024 * 1024)
    print(f"  - {file.name} ({size_mb:.2f} MB)")

print("\n📋 Next Steps:")
print("  1. Test the standalone EXE")
print("  2. Test the installer (if created)")
print("  3. Upload to release server/GitHub")
print("  4. Update JIRA task ASPS-16")
print()

# config_override.py is removed automatically by the atexit hook above.
