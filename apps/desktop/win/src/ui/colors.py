"""
Shared color constants for desktop UI.
Colors match extension popup.css for visual consistency.
"""

# Status colors (from extension background.js badge colors)
COLORS = {
    # Status states
    "green": "#22C55E",      # Connected/Protected
    "yellow": "#F59E0B",     # Warning/Reconnecting
    "red": "#EF4444",        # Disconnected/Dangerous
    "gray": "#666666",       # Unknown/Checking

    # Text colors
    "text_primary": "#FFFFFF",
    "text_muted": "#888888",

    # Background (dark theme)
    "bg_dark": "#1a1a2e",
    "bg_section": "#252540",

    # Separator
    "separator": "#333333",
}

# Icon colors for Pillow (RGBA tuples)
# Matches hex values above
ICON_COLORS = {
    "green": (34, 197, 94, 255),     # #22C55E
    "yellow": (245, 158, 11, 255),   # #F59E0B
    "red": (239, 68, 68, 255),       # #EF4444
    "gray": (102, 102, 102, 255),    # #666666
}
