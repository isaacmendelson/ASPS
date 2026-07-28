"""
Process-lifetime deadline watchdog and SIGTERM cleanup for the analyzer.

Responsibilities
----------------
- DeadlineWatchdog: arms a background timer that calls os._exit(1) if the
  analyzer's total execution time exceeds deadline_seconds.  This guarantees
  the Python process and therefore its Chromium child process tree cannot
  outlive the Backend's subprocess timeout.

- install_sigterm_handler: registers a POSIX SIGTERM handler that cleanly
  closes the Playwright browser before the process exits.  When the Backend
  calls process.Kill(entireProcessTree: true), the process tree is killed
  regardless — but on clean termination this gives Playwright a chance to
  release GPU/renderer child processes before the OS reclaims them.

Design notes
------------
- On Windows, SIGTERM is not a true signal; Python maps it to
  KeyboardInterrupt via SetConsoleCtrlHandler.  The watchdog uses os._exit(1)
  directly so it works on both platforms.
- DeadlineWatchdog.cancel() is idempotent and safe to call after the
  watchdog has already fired.
"""
from __future__ import annotations

import os
import signal
import sys
import threading
from typing import Callable, Optional


class DeadlineWatchdog:
    """
    Fire an action at most ``deadline_seconds`` after ``start()`` is called.

    Default action: os._exit(1) — hard exit that terminates every thread and
    causes the OS to clean up all child processes owned by this process.

    Usage::

        wdog = DeadlineWatchdog(deadline_seconds=25)
        wdog.start()
        try:
            do_work()
        finally:
            wdog.cancel()
    """

    def __init__(
        self,
        deadline_seconds: float,
        on_deadline: Optional[Callable[[], None]] = None,
    ) -> None:
        self._deadline = deadline_seconds
        self._on_deadline = on_deadline or self._default_terminate
        self._timer: Optional[threading.Timer] = None
        self._thread: Optional[threading.Thread] = None  # exposed for tests

    @staticmethod
    def _default_terminate() -> None:  # pragma: no cover — tested via mock
        os._exit(1)

    def start(self) -> None:
        """Arm the watchdog.  Must be called at most once."""
        timer = threading.Timer(self._deadline, self._on_deadline)
        timer.daemon = True
        timer.name = "analyzer-deadline-watchdog"
        self._thread = timer  # alias so tests can inspect .daemon
        self._timer = timer
        timer.start()

    def cancel(self) -> None:
        """Disarm the watchdog.  Safe to call multiple times."""
        if self._timer is not None:
            self._timer.cancel()


def install_sigterm_handler(scraper_state: dict) -> Callable:
    """
    Register a SIGTERM handler that closes Playwright resources before exit.

    ``scraper_state`` is a mutable dict with optional keys:
      - "browser": object with a ``.close()`` method
      - "playwright": object with a ``.stop()`` method

    The handler is also returned so callers can invoke it directly in tests.

    Note: on Windows Python only supports signal.SIGTERM via
    SetConsoleCtrlHandler; the handler is still registered but will only fire
    if the runtime converts the Windows control event.  The Backend's
    process.Kill(entireProcessTree: true) is the primary cleanup mechanism on
    Windows.
    """

    def _handler(signum: int, frame) -> None:  # noqa: ANN001
        # Close Playwright browser first — this sends a CDP shutdown command
        # and lets the browser flush before the OS kills the process.
        browser = scraper_state.get("browser")
        if browser is not None:
            try:
                browser.close()
            except Exception:
                pass

        playwright = scraper_state.get("playwright")
        if playwright is not None:
            try:
                playwright.stop()
            except Exception:
                pass

        # Exit with the conventional SIGTERM exit code on POSIX (128 + 15 = 143)
        # On Windows just use 1.
        sys.exit(143 if sys.platform != "win32" else 1)

    try:
        signal.signal(signal.SIGTERM, _handler)
    except (OSError, ValueError):
        # May fail if called from a non-main thread or on an unsupported platform.
        pass

    return _handler
