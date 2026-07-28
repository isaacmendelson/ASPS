"""Regression tests for the Chrome-extension WebSocket receive loop."""

import asyncio
import json
import os
import sys
import types
import unittest


# The repository's lightweight test environment may not have the optional
# WebSocket runtime installed.  These tests exercise ExtensionServer's
# connection orchestration directly, so a minimal import seam is sufficient.
_installed_websockets_stub = False
try:
    import websockets  # noqa: F401
except ModuleNotFoundError:
    _installed_websockets_stub = True
    websockets_module = types.ModuleType("websockets")
    websockets_server_module = types.ModuleType("websockets.server")

    class _ConnectionClosed(Exception):
        pass

    websockets_module.exceptions = types.SimpleNamespace(
        ConnectionClosed=_ConnectionClosed
    )
    websockets_server_module.WebSocketServerProtocol = object
    sys.modules["websockets"] = websockets_module
    sys.modules["websockets.server"] = websockets_server_module


sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

# ExtensionServer only needs this constant at import time. Avoid pulling the
# full desktop configuration (and its optional dotenv dependency) into this
# focused orchestration test.
_installed_config_stub = "config" not in sys.modules
if _installed_config_stub:
    config_module = types.ModuleType("config")
    config_module.EXTENSION_PORTS = []
    sys.modules["config"] = config_module

from extension_server import ExtensionServer

if _installed_config_stub:
    sys.modules.pop("config", None)
if _installed_websockets_stub:
    sys.modules.pop("websockets.server", None)
    sys.modules.pop("websockets", None)


class _LateResponseWebSocket:
    """Fake extension that replies only after the receive loop starts."""

    def __init__(self):
        self.sent_messages = []
        self._request_sent = asyncio.Event()
        self._finish = asyncio.Event()
        self._response_yielded = False

    def __aiter__(self):
        return self

    async def __anext__(self):
        if not self._response_yielded:
            await self._request_sent.wait()
            self._response_yielded = True
            request = self.sent_messages[-1]
            return json.dumps({
                "type": "browser_tabs_response",
                "requestId": request["requestId"],
                "tabs": [{"url": "https://example.com", "title": "Example"}],
            })

        await self._finish.wait()
        raise StopAsyncIteration

    async def send(self, payload):
        message = json.loads(payload)
        self.sent_messages.append(message)
        if message.get("type") == "get_browser_tabs":
            self._request_sent.set()

    async def close(self):
        self.finish()

    def finish(self):
        self._finish.set()


class _DisconnectingWebSocket:
    """Fake extension that disconnects while a tab request is pending."""

    def __init__(self):
        self._request_sent = asyncio.Event()
        self._disconnect = asyncio.Event()

    def __aiter__(self):
        return self

    async def __anext__(self):
        await self._request_sent.wait()
        await self._disconnect.wait()
        raise StopAsyncIteration

    async def send(self, payload):
        message = json.loads(payload)
        if message.get("type") == "get_browser_tabs":
            self._request_sent.set()

    async def wait_until_request_sent(self):
        await self._request_sent.wait()

    def disconnect(self):
        self._disconnect.set()


class TestExtensionServerConnectionOrdering(unittest.IsolatedAsyncioTestCase):
    async def test_receive_loop_handles_tabs_while_connect_callback_waits(self):
        """A late extension response must not deadlock its connect callback."""
        server = ExtensionServer()
        websocket = _LateResponseWebSocket()
        collected_tabs = []
        callback_done = asyncio.Event()

        async def refresh_remote_session_tabs():
            collected_tabs.extend(
                await server.request_browser_tabs(timeout=0.1)
            )
            callback_done.set()

        server.on_client_connect(refresh_remote_session_tabs)
        handler_task = asyncio.create_task(server._handle_client(websocket))

        await asyncio.wait_for(callback_done.wait(), timeout=0.5)

        self.assertEqual(
            collected_tabs,
            [{"url": "https://example.com", "title": "Example"}],
        )

        websocket.finish()
        await asyncio.wait_for(handler_task, timeout=0.5)

    async def test_disconnect_cleans_cancelled_tab_request_futures(self):
        """Cancelling the connect refresh must clean every pending request."""
        server = ExtensionServer()
        websocket = _DisconnectingWebSocket()

        async def refresh_remote_session_tabs():
            await server.request_browser_tabs(timeout=10.0)

        server.on_client_connect(refresh_remote_session_tabs)
        handler_task = asyncio.create_task(server._handle_client(websocket))

        await asyncio.wait_for(websocket.wait_until_request_sent(), timeout=0.5)
        pending_futures = list(server._tab_request_futures.values())
        self.assertEqual(len(pending_futures), 1)
        self.assertFalse(pending_futures[0].done())

        websocket.disconnect()
        await asyncio.wait_for(handler_task, timeout=0.5)

        self.assertEqual(server._tab_request_futures, {})
        self.assertTrue(all(future.cancelled() for future in pending_futures))


if __name__ == "__main__":
    unittest.main(verbosity=2)
