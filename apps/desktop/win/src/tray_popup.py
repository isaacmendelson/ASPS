"""
AntiScam Desktop App - Tray Popup Window
Tailscale-style popup UI for system tray icon.
"""

import logging
from typing import Callable, Optional

try:
    import customtkinter as ctk
    CTK_AVAILABLE = True
except ImportError:
    CTK_AVAILABLE = False

from ui.colors import COLORS

logger = logging.getLogger(__name__)


class TrayPopup(ctk.CTkToplevel):
    """
    Borderless popup window for system tray icon.

    Layout (top to bottom):
    1. Hero status row - most prominent
    2. Separator
    3. Remote Access section
    4. Separator
    5. System Status section
    6. Separator
    7. Footer actions

    Design: Tailscale-style, flat, single column, no scrolling.
    """

    WIDTH = 280
    HEIGHT = 320

    def __init__(
        self,
        master,
        on_exit: Optional[Callable] = None,
        on_preferences: Optional[Callable] = None,
        on_stop_session: Optional[Callable] = None
    ):
        super().__init__(master)

        # Store callbacks
        self._on_exit = on_exit
        self._on_preferences = on_preferences
        self._on_stop_session = on_stop_session

        # Hide before positioning to prevent flicker
        self.withdraw()

        # Configure window
        self.overrideredirect(True)  # Borderless
        self.configure(fg_color=COLORS["bg_dark"])
        self.geometry(f"{self.WIDTH}x{self.HEIGHT}")
        self.attributes("-topmost", True)  # Always on top

        # Slight transparency for modern look
        try:
            self.wm_attributes('-alpha', 0.97)
        except Exception:
            pass  # Not critical if fails

        # Store references to updatable widgets
        self._status_dot = None
        self._status_label = None
        self._remote_label = None
        self._stop_btn = None
        self._extension_value = None
        self._agent_value = None
        self._backend_value = None

        # Build UI sections
        self._build_status_row()
        self._add_separator()
        self._build_remote_section()
        self._add_separator()
        self._build_system_status()
        self._add_separator()
        self._build_footer()

        # Position near cursor and show
        self._position_near_cursor()
        self.deiconify()
        self.focus_force()

        # Close on focus loss
        self.bind("<FocusOut>", self._on_focus_out)

    def _position_near_cursor(self):
        """Position popup near the cursor, staying on screen."""
        # Get cursor position
        cursor_x = self.winfo_pointerx()
        cursor_y = self.winfo_pointery()

        # Get screen dimensions
        screen_width = self.winfo_screenwidth()
        screen_height = self.winfo_screenheight()

        # Calculate position (prefer above-left of cursor)
        x = cursor_x - self.WIDTH - 10
        y = cursor_y - self.HEIGHT - 10

        # Adjust if off screen (left edge)
        if x < 0:
            x = cursor_x + 10

        # Adjust if off screen (top edge)
        if y < 0:
            y = cursor_y + 10

        # Adjust if off screen (right edge)
        if x + self.WIDTH > screen_width:
            x = screen_width - self.WIDTH - 10

        # Adjust if off screen (bottom edge)
        if y + self.HEIGHT > screen_height:
            y = screen_height - self.HEIGHT - 10

        self.geometry(f"+{x}+{y}")

    def _build_status_row(self):
        """Build the hero protection status row."""
        frame = ctk.CTkFrame(self, fg_color="transparent")
        frame.pack(fill="x", padx=16, pady=(16, 12))

        # Status dot (12x12 circle)
        self._status_dot = ctk.CTkLabel(
            frame,
            text="",
            width=12,
            height=12,
            corner_radius=6,
            fg_color=COLORS["green"]
        )
        self._status_dot.pack(side="left", padx=(0, 10))

        # Status text - large and bold
        self._status_label = ctk.CTkLabel(
            frame,
            text="Protected",
            font=ctk.CTkFont(size=18, weight="bold"),
            text_color=COLORS["green"]
        )
        self._status_label.pack(side="left")

    def _add_separator(self):
        """Add a thin horizontal separator line."""
        separator = ctk.CTkFrame(
            self,
            height=1,
            fg_color=COLORS["separator"]
        )
        separator.pack(fill="x", padx=16, pady=8)

    def _build_remote_section(self):
        """Build the Remote Access section."""
        # Section frame
        self._remote_frame = ctk.CTkFrame(self, fg_color="transparent")
        self._remote_frame.pack(fill="x", padx=16, pady=0)

        # Section label
        section_label = ctk.CTkLabel(
            self._remote_frame,
            text="Remote Access",
            font=ctk.CTkFont(size=11),
            text_color=COLORS["text_muted"]
        )
        section_label.pack(anchor="w")

        # Status text
        self._remote_label = ctk.CTkLabel(
            self._remote_frame,
            text="No active sessions",
            font=ctk.CTkFont(size=13),
            text_color=COLORS["text_primary"]
        )
        self._remote_label.pack(anchor="w", pady=(4, 0))

        # Stop session button (hidden by default)
        self._stop_btn = ctk.CTkButton(
            self._remote_frame,
            text="Stop session",
            font=ctk.CTkFont(size=13, weight="bold"),
            fg_color=COLORS["red"],
            hover_color="#DC2626",  # Darker red on hover
            text_color=COLORS["text_primary"],
            height=36,
            corner_radius=6,
            command=self._handle_stop_session
        )
        # Don't pack yet - shown only when session active

    def _build_system_status(self):
        """Build the System Status section with 3 indicators."""
        frame = ctk.CTkFrame(self, fg_color="transparent")
        frame.pack(fill="x", padx=16, pady=0)

        # Section label
        section_label = ctk.CTkLabel(
            frame,
            text="System Status",
            font=ctk.CTkFont(size=11),
            text_color=COLORS["text_muted"]
        )
        section_label.pack(anchor="w", pady=(0, 4))

        # Status rows
        self._extension_value = self._add_status_row(frame, "Extension", "Connected")
        self._agent_value = self._add_status_row(frame, "Agent", "Running")
        self._backend_value = self._add_status_row(frame, "Backend", "Online")

    def _add_status_row(self, parent, label: str, initial_value: str) -> ctk.CTkLabel:
        """Helper to create a label-value status row."""
        row = ctk.CTkFrame(parent, fg_color="transparent")
        row.pack(fill="x", pady=2)

        # Label (left)
        label_widget = ctk.CTkLabel(
            row,
            text=label,
            font=ctk.CTkFont(size=11),
            text_color=COLORS["text_muted"],
            width=70,
            anchor="w"
        )
        label_widget.pack(side="left")

        # Value (right)
        value_widget = ctk.CTkLabel(
            row,
            text=initial_value,
            font=ctk.CTkFont(size=11),
            text_color=COLORS["green"],
            anchor="w"
        )
        value_widget.pack(side="left")

        return value_widget

    def _build_footer(self):
        """Build the footer with Preferences and Exit buttons."""
        frame = ctk.CTkFrame(self, fg_color="transparent")
        frame.pack(fill="x", padx=16, pady=12)

        # Preferences button (left)
        pref_btn = ctk.CTkButton(
            frame,
            text="Preferences",
            font=ctk.CTkFont(size=12),
            fg_color="transparent",
            text_color=COLORS["text_muted"],
            hover_color=COLORS["bg_section"],
            height=28,
            corner_radius=4,
            command=self._handle_preferences
        )
        pref_btn.pack(side="left")

        # Exit button (right)
        exit_btn = ctk.CTkButton(
            frame,
            text="Exit",
            font=ctk.CTkFont(size=12),
            fg_color="transparent",
            text_color=COLORS["text_muted"],
            hover_color=COLORS["bg_section"],
            height=28,
            corner_radius=4,
            command=self._handle_exit
        )
        exit_btn.pack(side="right")

    def _on_focus_out(self, event):
        """Handle focus loss - close popup after brief delay."""
        self.after(150, self._check_focus)

    def _check_focus(self):
        """Verify focus truly lost before closing."""
        try:
            focused = self.focus_get()
            # If popup or any child still has focus, don't close
            if focused is None or not str(focused).startswith(str(self)):
                self.destroy()
        except Exception:
            # Window already destroyed
            pass

    def _handle_stop_session(self):
        """Handle stop session button click."""
        if self._on_stop_session:
            self._on_stop_session()

    def _handle_preferences(self):
        """Handle preferences button click."""
        if self._on_preferences:
            self._on_preferences()

    def _handle_exit(self):
        """Handle exit button click."""
        if self._on_exit:
            self._on_exit()
        self.destroy()

    # Public update methods

    def set_protection_status(self, status: str, color: str):
        """
        Update the hero protection status.

        Args:
            status: Display text (e.g., "Protected", "Warning", "Remote control detected")
            color: Color key from COLORS (e.g., "green", "yellow", "red")
        """
        status_color = COLORS.get(color, COLORS["gray"])

        if self._status_dot:
            self._status_dot.configure(fg_color=status_color)

        if self._status_label:
            self._status_label.configure(text=status, text_color=status_color)

    def set_remote_access(self, tool_name: Optional[str] = None, direction: Optional[str] = None):
        """
        Update the Remote Access section.

        Args:
            tool_name: Name of active remote tool (e.g., "AnyDesk", "TeamViewer")
            direction: "Incoming" or "Outgoing"

        If tool_name is None, shows "No active sessions" and hides stop button.
        """
        if tool_name and direction:
            self._remote_label.configure(
                text=f"{tool_name} - {direction}",
                text_color=COLORS["yellow"] if direction == "Incoming" else COLORS["text_primary"]
            )
            # Show stop button
            self._stop_btn.pack(fill="x", pady=(8, 0))
        else:
            self._remote_label.configure(
                text="No active sessions",
                text_color=COLORS["text_primary"]
            )
            # Hide stop button
            self._stop_btn.pack_forget()

    def set_system_status(self, extension: bool, backend: bool):
        """
        Update the System Status indicators.

        Args:
            extension: True if extension is connected
            backend: True if backend is online
        """
        # Extension status
        if self._extension_value:
            self._extension_value.configure(
                text="Connected" if extension else "Disconnected",
                text_color=COLORS["green"] if extension else COLORS["red"]
            )

        # Agent is always running (since this app is running)
        if self._agent_value:
            self._agent_value.configure(
                text="Running",
                text_color=COLORS["green"]
            )

        # Backend status
        if self._backend_value:
            self._backend_value.configure(
                text="Online" if backend else "Offline",
                text_color=COLORS["green"] if backend else COLORS["red"]
            )


# Standalone testing
if __name__ == "__main__":
    import sys

    if not CTK_AVAILABLE:
        print("Error: customtkinter not installed")
        print("Run: pip install customtkinter")
        sys.exit(1)

    logging.basicConfig(level=logging.DEBUG)

    # Set dark theme
    ctk.set_appearance_mode("dark")

    # Create hidden root window
    root = ctk.CTk()
    root.withdraw()

    def on_exit():
        print("Exit clicked")
        root.quit()

    def on_preferences():
        print("Preferences clicked")

    def on_stop_session():
        print("Stop session clicked")
        # Reset to protected state after stopping
        popup.set_protection_status("Protected", "green")
        popup.set_remote_access(None, None)

    # Create popup
    popup = TrayPopup(
        root,
        on_exit=on_exit,
        on_preferences=on_preferences,
        on_stop_session=on_stop_session
    )

    # Demo sequence: cycle through states
    def show_warning_state():
        """Show warning state after 2 seconds."""
        popup.set_protection_status("Warning", "yellow")
        popup.set_remote_access("AnyDesk", "Incoming")
        popup.set_system_status(extension=True, backend=True)
        # Schedule danger state
        popup.after(3000, show_danger_state)

    def show_danger_state():
        """Show danger state after 3 more seconds."""
        popup.set_protection_status("Remote control detected", "red")
        popup.set_remote_access("TeamViewer", "Incoming")
        popup.set_system_status(extension=True, backend=False)
        # Schedule disconnected state
        popup.after(3000, show_disconnected_state)

    def show_disconnected_state():
        """Show disconnected state after 3 more seconds."""
        popup.set_protection_status("Not protected", "gray")
        popup.set_remote_access(None, None)
        popup.set_system_status(extension=False, backend=False)

    # Start demo sequence
    popup.after(2000, show_warning_state)

    print("TrayPopup test running...")
    print("Watch the popup cycle through states: Protected -> Warning -> Danger -> Disconnected")
    print("Click outside the popup to close it, or use the Exit button.")

    # Run mainloop
    root.mainloop()
