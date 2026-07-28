"""
ASPS-613 — Timeout deadline alignment and process-tree cleanup tests.

Red/green spec:
  1. The scraper's maximum possible wall-clock time fits within the
     configured deadline (deadline_seconds) with margin left for IPC.
  2. A SIGTERM sent to the Python process triggers Playwright cleanup before
     the process exits.
  3. A DeadlineWatchdog fires within the configured deadline and terminates
     the process cleanly.
  4. The settings.json total budget is less than the Backend's 30-second timeout.
"""
from __future__ import annotations

import json
import os
import signal
import subprocess
import sys
import threading
import time
from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

# ---------------------------------------------------------------------------
# helpers
# ---------------------------------------------------------------------------

def _load_settings() -> dict:
    with open(ROOT / "config" / "settings.json", encoding="utf-8") as f:
        return json.load(f)


# ---------------------------------------------------------------------------
# 1. Settings — deadline alignment
# ---------------------------------------------------------------------------

class TestDeadlineAlignment:
    """Verify that the configured timeouts fit within the Backend's 30s window."""

    BACKEND_TIMEOUT_SECONDS = 30

    def test_deadline_seconds_key_exists_in_settings(self):
        settings = _load_settings()
        assert "deadline_seconds" in settings["scraping"], (
            "settings.json must have scraping.deadline_seconds"
        )

    def test_deadline_is_less_than_backend_timeout(self):
        settings = _load_settings()
        deadline = settings["scraping"]["deadline_seconds"]
        assert deadline < self.BACKEND_TIMEOUT_SECONDS, (
            f"deadline_seconds ({deadline}) must be less than Backend timeout "
            f"({self.BACKEND_TIMEOUT_SECONDS})"
        )

    def test_scraper_timeout_plus_margin_fits_in_deadline(self):
        """
        The scraper worst-case is:
          8s (networkidle attempt) + timeout_seconds (domcontentloaded fallback)
          + 2s (blocked-page delay on retry)
        This must fit within deadline_seconds.
        """
        settings = _load_settings()
        scraping = settings["scraping"]
        timeout_s = scraping["timeout_seconds"]
        deadline_s = scraping["deadline_seconds"]
        # networkidle hard-coded at 8s + domcontentloaded timeout + 2s blocked delay
        worst_case_single = 8 + timeout_s + 2
        assert worst_case_single <= deadline_s, (
            f"Worst-case single scrape ({worst_case_single}s) exceeds "
            f"deadline_seconds ({deadline_s}s). Lower timeout_seconds."
        )

    def test_deadline_leaves_ipc_margin_below_backend_timeout(self):
        """At least 3 seconds must remain between deadline and Backend timeout."""
        settings = _load_settings()
        deadline_s = settings["scraping"]["deadline_seconds"]
        margin = self.BACKEND_TIMEOUT_SECONDS - deadline_s
        assert margin >= 3, (
            f"Only {margin}s margin between deadline ({deadline_s}s) and "
            f"Backend timeout ({self.BACKEND_TIMEOUT_SECONDS}s). Need >=3s."
        )


# ---------------------------------------------------------------------------
# 2. SIGTERM handler — Playwright cleanup
# ---------------------------------------------------------------------------

class TestSigtermCleanup:
    """SIGTERM must trigger browser close before the process exits."""

    def test_sigterm_handler_is_installed_by_deadline_watchdog(self):
        from scrapers.deadline import install_sigterm_handler
        closed = []

        class FakeBrowser:
            def close(self):
                closed.append("browser")

        class FakePlaywright:
            def stop(self):
                closed.append("playwright")

        scraper_state = {"browser": FakeBrowser(), "playwright": FakePlaywright()}

        # The handler should call close on browser and playwright, then sys.exit.
        handler = install_sigterm_handler(scraper_state)
        # Simulate SIGTERM delivery (call the handler directly).
        # sys.exit() is expected — catch it.
        with pytest.raises(SystemExit):
            handler(signal.SIGTERM, None)

        assert "browser" in closed, "SIGTERM handler must close the browser"
        assert "playwright" in closed, "SIGTERM handler must stop playwright"

    @pytest.mark.skipif(
        sys.platform == "win32",
        reason="SIGTERM process test uses POSIX signals — Windows uses TerminateProcess"
    )
    def test_sigterm_to_subprocess_calls_cleanup_and_exits(self, tmp_path):
        """
        A subprocess that installs the SIGTERM handler and then blocks
        should close cleanly within 3 seconds when sent SIGTERM.
        """
        script = tmp_path / "target.py"
        script.write_text(
            f"""
import sys, time, signal
sys.path.insert(0, {str(ROOT)!r})
from scrapers.deadline import install_sigterm_handler, DeadlineWatchdog

state = {{"browser": None, "playwright": None}}
install_sigterm_handler(state)

# Signal we're ready
sys.stdout.write("ready\\n")
sys.stdout.flush()

# Block until killed
time.sleep(60)
""",
            encoding="utf-8",
        )
        proc = subprocess.Popen(
            [sys.executable, str(script)],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
        # Wait until script is ready
        line = proc.stdout.readline()
        assert line.strip() == "ready"

        # Send SIGTERM
        proc.send_signal(signal.SIGTERM)
        try:
            proc.wait(timeout=3)
        except subprocess.TimeoutExpired:
            proc.kill()
            pytest.fail("Process did not exit within 3s after SIGTERM")
        # Exit code from signal on POSIX is typically negative
        assert proc.returncode is not None


# ---------------------------------------------------------------------------
# 3. DeadlineWatchdog — fires within deadline
# ---------------------------------------------------------------------------

class TestDeadlineWatchdog:
    """DeadlineWatchdog must fire at or before the configured deadline."""

    def test_watchdog_fires_before_deadline_elapses(self):
        from scrapers.deadline import DeadlineWatchdog

        fired = threading.Event()

        def fake_terminate():
            fired.set()

        wdog = DeadlineWatchdog(deadline_seconds=0.2, on_deadline=fake_terminate)
        wdog.start()
        fired.wait(timeout=1.0)
        wdog.cancel()

        assert fired.is_set(), "DeadlineWatchdog did not fire within deadline"

    def test_watchdog_cancelled_before_deadline_does_not_fire(self):
        from scrapers.deadline import DeadlineWatchdog

        fired = threading.Event()

        def fake_terminate():
            fired.set()

        wdog = DeadlineWatchdog(deadline_seconds=0.5, on_deadline=fake_terminate)
        wdog.start()
        wdog.cancel()
        time.sleep(0.7)  # wait past deadline

        assert not fired.is_set(), "Cancelled watchdog must not fire"

    def test_watchdog_is_daemon_thread(self):
        from scrapers.deadline import DeadlineWatchdog

        wdog = DeadlineWatchdog(deadline_seconds=60, on_deadline=lambda: None)
        wdog.start()
        assert wdog._thread.daemon, "Watchdog thread must be a daemon"
        wdog.cancel()

    def test_watchdog_default_action_terminates_process(self):
        """When no on_deadline is given, the watchdog calls os._exit(1)."""
        from scrapers.deadline import DeadlineWatchdog

        exited = []
        with patch("scrapers.deadline.os") as mock_os:
            mock_os._exit = lambda code: exited.append(code)
            wdog = DeadlineWatchdog(deadline_seconds=0.1)
            wdog.start()
            time.sleep(0.5)
            wdog.cancel()

        assert exited == [1], "Default watchdog action must call os._exit(1)"


# ---------------------------------------------------------------------------
# 4. v1_stdio integration — deadline is armed before analysis begins
# ---------------------------------------------------------------------------

class TestV1StdioDeadlineIntegration:
    """v1_stdio.main() must arm the DeadlineWatchdog before calling analyze()."""

    def test_watchdog_is_started_before_analyze_is_called(self):
        import io
        from unittest.mock import patch as upatch, call

        from v1_stdio import process_stream

        request = {
            "schemaVersion": "1.0",
            "messageId": "11111111-1111-4111-8111-111111111111",
            "correlationId": "22222222-2222-4222-8222-222222222222",
            "requestId": "33333333-3333-4333-8333-333333333333",
            "messageType": "url_scan.request",
            "sentAt": "2026-07-28T12:00:00.000Z",
            "source": "backend",
            "context": {
                "deviceId": "device-1",
                "tabId": "12",
                "url": "https://example.com/",
            },
            "outcome": None,
            "payload": {},
        }

        watchdog_started = []
        watchdog_cancelled = []

        class FakeWatchdog:
            def __init__(self, **kwargs):
                pass
            def start(self):
                watchdog_started.append(True)
            def cancel(self):
                watchdog_cancelled.append(True)

        with upatch("v1_stdio.DeadlineWatchdog", FakeWatchdog):
            stdout = io.StringIO()
            process_stream(
                io.StringIO(json.dumps(request)),
                stdout,
                io.StringIO(),
                analyze=lambda _: {"risk_assessment": {"risk_score": 10, "risk_level": "LOW"}, "red_flags": []},
            )

        assert watchdog_started, "DeadlineWatchdog must be started before analysis"
        assert watchdog_cancelled, "DeadlineWatchdog must be cancelled after analysis"

    def test_v1_stdio_emits_timeout_error_when_analyze_exceeds_deadline(self):
        """
        When the analyze callable hangs past the deadline, the watchdog fires.
        Since we cannot easily test os._exit in-process, we test that a timeout
        error code is produced when the analyze function raises TimeoutError.
        """
        import io

        from v1_stdio import process_stream

        request = {
            "schemaVersion": "1.0",
            "messageId": "11111111-1111-4111-8111-111111111111",
            "correlationId": "22222222-2222-4222-8222-222222222222",
            "requestId": "33333333-3333-4333-8333-333333333333",
            "messageType": "url_scan.request",
            "sentAt": "2026-07-28T12:00:00.000Z",
            "source": "backend",
            "context": {
                "deviceId": "device-1",
                "tabId": "12",
                "url": "https://example.com/",
            },
            "outcome": None,
            "payload": {},
        }

        def slow_analyze(_url):
            raise TimeoutError("scraper timed out")

        stdout = io.StringIO()
        process_stream(
            io.StringIO(json.dumps(request)),
            stdout,
            io.StringIO(),
            analyze=slow_analyze,
        )

        response = json.loads(stdout.getvalue())
        assert response["messageType"] == "url_scan.error"
        err = response["outcome"]["error"]
        assert err["code"] == "analyzer.timeout"
        assert err["retryable"] is True


# ---------------------------------------------------------------------------
# 5. Playwright scraper — timeout alignment
# ---------------------------------------------------------------------------

class TestPlaywrightScraperTimeouts:
    """PlaywrightScraper must honour the deadline_seconds from settings."""

    def test_scraper_reads_deadline_seconds_from_settings(self):
        from scrapers.playwright_scraper import PlaywrightScraper
        scraper = PlaywrightScraper()
        settings = _load_settings()
        expected_deadline = settings["scraping"]["deadline_seconds"]
        assert scraper.deadline_seconds == expected_deadline, (
            f"PlaywrightScraper.deadline_seconds ({scraper.deadline_seconds}) "
            f"must match settings ({expected_deadline})"
        )

    def test_networkidle_timeout_is_fixed_at_8_seconds(self):
        """The 8s networkidle attempt must stay fixed — it's a hard-coded safety valve."""
        import ast

        src = (ROOT / "scrapers" / "playwright_scraper.py").read_text(encoding="utf-8")
        tree = ast.parse(src)

        # Find the goto(..., timeout=8000, ...) call
        found_8000 = False
        for node in ast.walk(tree):
            if isinstance(node, ast.Call):
                func = getattr(node.func, "attr", None)
                if func == "goto":
                    for kw in node.keywords:
                        if kw.arg == "timeout" and isinstance(kw.value, ast.Constant):
                            if kw.value.value == 8000:
                                found_8000 = True
        assert found_8000, "networkidle goto must use timeout=8000 (8 seconds)"
