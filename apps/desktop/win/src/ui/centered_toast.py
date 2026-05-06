"""
CenteredToast
-------------

Borderless, always-on-top, draggable alert window centered on the primary
monitor. Used for ImmediateDanger and other high-attention alerts where
Windows' bottom-right toast is not prominent enough.

Two modes:
  - 'locked' (default for ImmediateDanger): no close button, no auto-dismiss,
             always-on-top with periodic lift, draggable. Persists until the
             event clears (server sends ImmediateDangerEnded).
  - 'cleared' (transform target): green styling, close button, optional
             auto-dismiss.

Singleton: at most one CenteredToast is on screen at any time. Subsequent
`show_locked()` calls update the existing window in place. `transform_to_cleared()`
turns the active window from red→green and adds a close button.

Threading: tkinter operations must happen on the main UI thread. Public
functions accept a tkinter root and schedule via `root.after(0, ...)`.
"""

import logging
import threading
from typing import Callable, Optional

try:
    import customtkinter as ctk
    CTK_AVAILABLE = True
except ImportError:
    CTK_AVAILABLE = False

from ui.colors import COLORS

logger = logging.getLogger(__name__)


# Risk-level → border/accent color
RISK_COLOR = {
    "critical": COLORS["red"],
    "high":     COLORS["red"],
    "medium":   COLORS["yellow"],
    "low":      COLORS["yellow"],
    "none":     COLORS["green"],
}

RISK_ICON = {
    "critical": "[!]",
    "high":     "[!]",
    "medium":   "[!]",
    "low":      "[i]",
    "none":     "[ok]",
}


# ─── Singleton state ─────────────────────────────────────────────────────────
_active_alert: Optional["CenteredToast"] = None
_active_lock = threading.Lock()


def _set_active(alert: Optional["CenteredToast"]) -> None:
    global _active_alert
    with _active_lock:
        _active_alert = alert


def _get_active() -> Optional["CenteredToast"]:
    with _active_lock:
        return _active_alert


# ─── Window class ────────────────────────────────────────────────────────────
class CenteredToast(ctk.CTkToplevel):
    """Centered, always-on-top, draggable alert window with two modes."""

    WIDTH = 520
    HEIGHT = 260

    LIFT_INTERVAL_MS = 2000  # how often to call lift() to defeat Windows topmost loss

    def __init__(
        self,
        master,
        title: str,
        message: str,
        risk_level: str = "critical",
        mode: str = "locked",
        auto_dismiss_seconds: int = 0,
        on_view_details: Optional[Callable] = None,
    ):
        super().__init__(master)

        self.withdraw()  # build then reveal

        self._mode = mode
        self._auto_dismiss_seconds = max(0, int(auto_dismiss_seconds))
        self._remaining_ms = self._auto_dismiss_seconds * 1000
        self._tick_after_id: Optional[str] = None
        self._lift_after_id: Optional[str] = None
        self._on_view_details_cb = on_view_details

        # Window chrome
        self.overrideredirect(True)               # Borderless (also blocks resize/Alt+F4)
        self.attributes("-topmost", True)
        self.configure(fg_color=COLORS["bg_dark"])
        self.geometry(f"{self.WIDTH}x{self.HEIGHT}")
        try:
            self.wm_attributes("-alpha", 0.97)
        except Exception:
            pass
        # Prevent Windows from accepting Alt+F4 on this borderless window
        self.protocol("WM_DELETE_WINDOW", self._block_close)

        # Drag state
        self._drag_x = 0
        self._drag_y = 0

        # Build UI
        self._stripe = ctk.CTkFrame(self, height=6, corner_radius=0,
                                    fg_color=self._color())
        self._stripe.pack(fill="x", side="top")

        self._header = ctk.CTkFrame(self, fg_color="transparent")
        self._header.pack(fill="x", padx=20, pady=(16, 8))

        self._icon_label = ctk.CTkLabel(
            self._header,
            text=self._icon(),
            font=ctk.CTkFont(size=24, weight="bold"),
            text_color=self._color(),
        )
        self._icon_label.pack(side="left", padx=(0, 12))

        self._title_label = ctk.CTkLabel(
            self._header,
            text=title,
            font=ctk.CTkFont(size=18, weight="bold"),
            text_color=COLORS["text_primary"],
            anchor="w",
        )
        self._title_label.pack(side="left", fill="x", expand=True)

        # Drag-handle indicator on the right of the header
        self._drag_hint = ctk.CTkLabel(
            self._header,
            text="≡",
            font=ctk.CTkFont(size=16),
            text_color=COLORS["text_muted"],
        )
        self._drag_hint.pack(side="right")

        self._message_label = ctk.CTkLabel(
            self,
            text=message,
            font=ctk.CTkFont(size=13),
            text_color=COLORS["text_primary"],
            wraplength=self.WIDTH - 40,
            justify="left",
            anchor="w",
        )
        self._message_label.pack(fill="x", padx=20, pady=(0, 16))

        self._buttons = ctk.CTkFrame(self, fg_color="transparent")
        self._buttons.pack(fill="x", padx=20, pady=(0, 12), side="top")

        # View Details button — present in both modes
        self._view_btn = ctk.CTkButton(
            self._buttons,
            text="View Details",
            command=self._handle_view_details,
            fg_color=self._color(),
            hover_color=COLORS["bg_section"],
            text_color=COLORS["text_primary"],
            corner_radius=8,
            height=34,
        )
        self._view_btn.pack(side="right", padx=(8, 0))

        # Close button — only present in 'cleared' mode
        self._close_btn: Optional[ctk.CTkButton] = None
        if mode == "cleared":
            self._add_close_button()

        # Countdown bar (drives auto-dismiss in 'cleared' mode only)
        self._countdown = ctk.CTkProgressBar(
            self,
            height=4,
            corner_radius=0,
            progress_color=self._color(),
            fg_color=COLORS["bg_section"],
        )
        self._countdown.pack(fill="x", side="bottom")
        self._countdown.set(1.0)

        # Drag bindings on the header (whole row, including the icon/title/hint)
        for w in (self._header, self._icon_label, self._title_label, self._drag_hint, self._stripe):
            w.bind("<Button-1>", self._start_drag)
            w.bind("<B1-Motion>", self._do_drag)

        # Center on the primary monitor
        self.update_idletasks()
        sw = self.winfo_screenwidth()
        sh = self.winfo_screenheight()
        x = (sw - self.WIDTH) // 2
        y = (sh - self.HEIGHT) // 3
        self.geometry(f"{self.WIDTH}x{self.HEIGHT}+{x}+{y}")

        # Reveal
        self.deiconify()
        try:
            self.lift()
            self.focus_force()
        except Exception:
            pass

        # Always-on-top enforcement loop
        self._lift_loop()

        # Auto-dismiss timer (cleared mode + auto_dismiss_seconds>0)
        if self._mode == "cleared" and self._auto_dismiss_seconds > 0:
            self._tick()

    # ─── Mode helpers ────────────────────────────────────────────────────
    def _color(self) -> str:
        return RISK_COLOR.get(getattr(self, "_risk_level", "critical"), COLORS["red"]) \
            if hasattr(self, "_risk_level") else RISK_COLOR.get("critical", COLORS["red"])

    def _icon(self) -> str:
        return RISK_ICON.get(getattr(self, "_risk_level", "critical"), "[!]") \
            if hasattr(self, "_risk_level") else RISK_ICON.get("critical", "[!]")

    def _block_close(self):
        """Block WM_DELETE_WINDOW (Alt+F4 etc) when in locked mode."""
        if self._mode == "locked":
            return  # ignore
        self._close()

    def _add_close_button(self):
        if self._close_btn is not None:
            return
        self._close_btn = ctk.CTkButton(
            self._buttons,
            text="Close",
            command=self._close,
            fg_color=COLORS["bg_section"],
            hover_color=COLORS["separator"],
            text_color=COLORS["text_primary"],
            corner_radius=8,
            height=34,
        )
        self._close_btn.pack(side="right")

    # ─── Always-on-top enforcement ───────────────────────────────────────
    def _lift_loop(self):
        if not self.winfo_exists():
            return
        try:
            self.attributes("-topmost", True)
            self.lift()
        except Exception:
            pass
        self._lift_after_id = self.after(self.LIFT_INTERVAL_MS, self._lift_loop)

    # ─── Drag ────────────────────────────────────────────────────────────
    def _start_drag(self, event):
        self._drag_x = event.x_root - self.winfo_x()
        self._drag_y = event.y_root - self.winfo_y()

    def _do_drag(self, event):
        new_x = event.x_root - self._drag_x
        new_y = event.y_root - self._drag_y
        try:
            self.geometry(f"+{new_x}+{new_y}")
        except Exception:
            pass

    # ─── Cleared-mode auto-dismiss ───────────────────────────────────────
    def _tick(self):
        if not self.winfo_exists():
            return
        tick_ms = 100
        self._remaining_ms -= tick_ms
        if self._remaining_ms <= 0:
            self._close()
            return
        try:
            progress = self._remaining_ms / (self._auto_dismiss_seconds * 1000)
            self._countdown.set(max(0.0, progress))
        except Exception:
            pass
        self._tick_after_id = self.after(tick_ms, self._tick)

    # ─── Buttons ─────────────────────────────────────────────────────────
    def _handle_view_details(self):
        if self._on_view_details_cb:
            try:
                self._on_view_details_cb()
            except Exception as e:
                logger.error(f"on_view_details callback failed: {e}")

    def _close(self):
        # Cancel timers
        for after_id_attr in ("_tick_after_id", "_lift_after_id"):
            after_id = getattr(self, after_id_attr, None)
            if after_id:
                try:
                    self.after_cancel(after_id)
                except Exception:
                    pass
                setattr(self, after_id_attr, None)
        try:
            self.destroy()
        finally:
            if _get_active() is self:
                _set_active(None)

    # ─── Public: in-place update for transform_to_cleared ────────────────
    def update_content(
        self,
        title: str,
        message: str,
        risk_level: str,
        mode: str,
        auto_dismiss_seconds: int = 0,
    ):
        """Update title, message, color, and mode without re-creating the window."""
        self._mode = mode
        self._risk_level = risk_level
        color = RISK_COLOR.get(risk_level, COLORS["red"])
        icon = RISK_ICON.get(risk_level, "[!]")

        try:
            self._title_label.configure(text=title)
            self._message_label.configure(text=message)
            self._icon_label.configure(text=icon, text_color=color)
            self._stripe.configure(fg_color=color)
            self._view_btn.configure(fg_color=color)
            self._countdown.configure(progress_color=color)
            self._countdown.set(1.0)
        except Exception as e:
            logger.error(f"update_content failed: {e}")
            return

        # Add Close button if entering cleared mode
        if mode == "cleared":
            self._add_close_button()
            # Optionally start auto-dismiss
            self._auto_dismiss_seconds = max(0, int(auto_dismiss_seconds))
            self._remaining_ms = self._auto_dismiss_seconds * 1000
            if self._auto_dismiss_seconds > 0 and not self._tick_after_id:
                self._tick()

        # Re-assert always-on-top
        try:
            self.attributes("-topmost", True)
            self.lift()
        except Exception:
            pass


# ─── Public API (thread-safe) ────────────────────────────────────────────────
def show_locked(
    root,
    title: str,
    message: str,
    risk_level: str = "critical",
    on_view_details: Optional[Callable] = None,
) -> bool:
    """Show or update the singleton LOCKED alert.

    If a CenteredToast is already on screen, its content is updated in-place.
    Otherwise a new locked window is created. Returns True on schedule success.
    """
    if not CTK_AVAILABLE or root is None:
        return False

    def _build():
        existing = _get_active()
        if existing is not None and existing.winfo_exists():
            existing.update_content(title=title, message=message,
                                    risk_level=risk_level, mode="locked",
                                    auto_dismiss_seconds=0)
            return
        toast = CenteredToast(
            root, title=title, message=message,
            risk_level=risk_level, mode="locked",
            auto_dismiss_seconds=0,
            on_view_details=on_view_details,
        )
        toast._risk_level = risk_level  # track current risk for color helpers
        _set_active(toast)

    try:
        root.after(0, _build)
        return True
    except Exception as e:
        logger.error(f"show_locked schedule failed: {e}")
        return False


def transform_to_cleared(
    root,
    title: str,
    message: str,
    auto_dismiss_seconds: int = 0,
    on_view_details: Optional[Callable] = None,
) -> bool:
    """Transform the active locked alert into the CLEARED (green) state.

    If no alert is currently shown, creates a fresh cleared alert.
    auto_dismiss_seconds=0 means the user must click Close to dismiss.
    """
    if not CTK_AVAILABLE or root is None:
        return False

    def _build():
        existing = _get_active()
        if existing is not None and existing.winfo_exists():
            existing.update_content(title=title, message=message,
                                    risk_level="none", mode="cleared",
                                    auto_dismiss_seconds=auto_dismiss_seconds)
            return
        toast = CenteredToast(
            root, title=title, message=message,
            risk_level="none", mode="cleared",
            auto_dismiss_seconds=auto_dismiss_seconds,
            on_view_details=on_view_details,
        )
        toast._risk_level = "none"
        _set_active(toast)

    try:
        root.after(0, _build)
        return True
    except Exception as e:
        logger.error(f"transform_to_cleared schedule failed: {e}")
        return False


def close_active(root) -> None:
    """Force-close the active alert (regardless of mode). For shutdown only."""
    if root is None:
        return
    def _close():
        existing = _get_active()
        if existing is not None and existing.winfo_exists():
            try:
                existing._close()
            except Exception:
                pass
    try:
        root.after(0, _close)
    except Exception:
        pass


# Backwards-compatible alias used by earlier tray_icon.show_centered_alert calls.
# Default behavior is LOCKED mode (auto_dismiss_seconds intentionally ignored
# for ImmediateDanger contexts).
def show(
    root,
    title: str,
    message: str,
    risk_level: str = "critical",
    auto_dismiss_seconds: int = 0,
    on_view_details: Optional[Callable] = None,
    on_dismiss: Optional[Callable] = None,
) -> bool:
    if risk_level == "none":
        return transform_to_cleared(
            root, title, message,
            auto_dismiss_seconds=auto_dismiss_seconds,
            on_view_details=on_view_details,
        )
    return show_locked(
        root, title, message,
        risk_level=risk_level,
        on_view_details=on_view_details,
    )
