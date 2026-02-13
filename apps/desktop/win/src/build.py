"""
Build script for AntiScam Desktop App
Creates a standalone EXE using PyInstaller
"""

import PyInstaller.__main__
import os
import shutil

# Paths
SRC_DIR = os.path.dirname(os.path.abspath(__file__))
DIST_DIR = os.path.join(SRC_DIR, '..', 'dist')
BUILD_DIR = os.path.join(SRC_DIR, '..', 'build')

# Clean previous builds
if os.path.exists(DIST_DIR):
    shutil.rmtree(DIST_DIR)
if os.path.exists(BUILD_DIR):
    shutil.rmtree(BUILD_DIR)

# PyInstaller arguments
args = [
    os.path.join(SRC_DIR, 'main.py'),
    '--name=AntiScam',
    '--onefile',              # Single EXE file
    '--windowed',             # No console window
    '--clean',
    f'--distpath={DIST_DIR}',
    f'--workpath={BUILD_DIR}',
    # Add icon if you have one:
    # '--icon=icon.ico',
]

print("Building AntiScam Desktop App...")
print("=" * 50)

PyInstaller.__main__.run(args)

print("=" * 50)
print(f"Build complete! EXE is in: {DIST_DIR}")
