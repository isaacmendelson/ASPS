"""
AntiScam Desktop App - Hardware Device ID Generator

Generates a stable, unique device ID from hardware identifiers using
PowerShell Get-CimInstance. Caches the result to disk so it survives
restarts. Falls back to Windows MachineGuid when hardware serials are
generic/placeholder values.

Public API:
    get_or_create_device_id() -> str   # Returns "PC-{16 hex chars}"
"""

import subprocess
import hashlib
import winreg
import logging
import platform
import os
from pathlib import Path

from config import DATA_DIR

logger = logging.getLogger(__name__)

# Known generic/placeholder values returned by OEM hardware
GENERIC_VALUES = {
    "", "none", "not applicable", "to be filled by o.e.m.",
    "default string", "default", "system serial number",
    "system product name", "0", "123456789",
    "ffffffff-ffff-ffff-ffff-ffffffffffff",
    "not specified", "chassis serial number",
}


def _is_generic(value: str) -> bool:
    """Check if a hardware value is a known generic/placeholder."""
    return value.strip().lower() in GENERIC_VALUES


def _query_hardware() -> tuple[str, str, str]:
    """Query motherboard, BIOS, and disk serial in a single PowerShell call.

    Returns:
        Tuple of (motherboard_serial, bios_serial, disk_serial).
        Returns ("", "", "") on any failure.
    """
    cmd = (
        "(Get-CimInstance Win32_BaseBoard).SerialNumber; "
        "(Get-CimInstance Win32_BIOS).SerialNumber; "
        "(Get-CimInstance Win32_DiskDrive | Sort-Object Index "
        "| Select-Object -First 1).SerialNumber"
    )
    try:
        result = subprocess.run(
            ["powershell", "-NoProfile", "-Command", cmd],
            capture_output=True,
            text=True,
            timeout=10,
        )
        if result.returncode == 0:
            lines = result.stdout.strip().split("\n")
            mobo = lines[0].strip() if len(lines) > 0 else ""
            bios = lines[1].strip() if len(lines) > 1 else ""
            disk = lines[2].strip() if len(lines) > 2 else ""
            logger.debug(
                "Hardware query results: mobo=%r, bios=%r, disk=%r",
                mobo, bios, disk,
            )
            return mobo, bios, disk
    except subprocess.TimeoutExpired:
        logger.warning("PowerShell hardware query timed out after 10s")
    except FileNotFoundError:
        logger.warning("PowerShell not found on this system")
    except OSError as exc:
        logger.warning("PowerShell hardware query failed: %s", exc)
    return ("", "", "")


def _get_machine_guid() -> str:
    """Read MachineGuid from the Windows registry (fallback).

    Returns:
        The MachineGuid string, or empty string on failure.
    """
    try:
        key = winreg.OpenKey(
            winreg.HKEY_LOCAL_MACHINE,
            r"SOFTWARE\Microsoft\Cryptography",
        )
        try:
            guid, _ = winreg.QueryValueEx(key, "MachineGuid")
            return guid
        finally:
            winreg.CloseKey(key)
    except Exception:
        logger.warning("Failed to read MachineGuid from registry")
        return ""


def _generate_device_id() -> str:
    """Generate a device ID in the format PC-{16 hex chars}.

    Strategy:
        1. Query hardware serials (mobo, BIOS, disk) via Get-CimInstance
        2. Filter out generic/placeholder values
        3. If all are generic -> fall back to MachineGuid
        4. If MachineGuid also fails -> last resort platform info
        5. SHA-256 hash the combined string, take first 16 hex chars
    """
    mobo, bios, disk = _query_hardware()

    # Filter generic values
    mobo = "" if _is_generic(mobo) else mobo
    bios = "" if _is_generic(bios) else bios
    disk = "" if _is_generic(disk) else disk

    if not mobo and not bios and not disk:
        # All hardware values are generic -- use MachineGuid fallback
        guid = _get_machine_guid()
        if guid:
            combined = f"MachineGuid:{guid}"
            logger.info("Using MachineGuid fallback for device ID")
        else:
            # Last resort
            combined = f"fallback:{platform.node()}:{platform.machine()}"
            logger.warning("Using platform fallback for device ID (no hardware or MachineGuid)")
    else:
        combined = f"{mobo}|{bios}|{disk}"

    hash_hex = hashlib.sha256(combined.encode("utf-8")).hexdigest()
    return f"PC-{hash_hex[:16]}"


def get_or_create_device_id() -> str:
    """Load cached device ID from disk, or generate from hardware and cache it.

    This is the single public entry point for the module.

    Returns:
        A device ID string matching the pattern PC-[0-9a-f]{16}.
    """
    cache_path = Path(DATA_DIR) / "device_id"

    # Try loading from cache
    if cache_path.exists():
        try:
            cached = cache_path.read_text(encoding="utf-8").strip()
            if cached:
                logger.info("Loaded cached device ID: %s", cached)
                return cached
        except OSError as exc:
            logger.warning("Failed to read device ID cache: %s", exc)

    # Generate from hardware
    device_id = _generate_device_id()

    # Cache to disk
    try:
        cache_path.parent.mkdir(parents=True, exist_ok=True)
        cache_path.write_text(device_id, encoding="utf-8")
        logger.info("Generated and cached device ID: %s", device_id)
    except OSError as exc:
        logger.warning("Failed to write device ID cache: %s", exc)
        logger.info("Generated device ID (in-memory only): %s", device_id)

    return device_id


if __name__ == "__main__":
    logging.basicConfig(level=logging.DEBUG)
    device_id = get_or_create_device_id()
    print(f"Device ID: {device_id}")
