"""
AntiScam Desktop App - Extension Server
WebSocket server for Chrome extension communication
"""

import asyncio
import json
import logging
import uuid
from typing import Optional, Callable, Dict, List, Set, Tuple
import websockets
from websockets.server import WebSocketServerProtocol

from config import EXTENSION_PORTS

logger = logging.getLogger(__name__)


class ExtensionServer:
    """WebSocket server for extension communication"""
    
    def __init__(self):
        self.port: Optional[int] = None
        self.server = None
        self.clients: Set[WebSocketServerProtocol] = set()
        self._on_message_callback: Optional[Callable] = None
        self._on_client_connect_callback: Optional[Callable] = None
        self._running = False
        # Pending tab requests: key = "{request_id}:{client_id}" → Future[List[dict]]
        self._tab_request_futures: Dict[str, asyncio.Future] = {}

    def on_message(self, callback: Callable):
        """Set callback for incoming messages"""
        self._on_message_callback = callback

    def on_client_connect(self, callback: Callable):
        """Register an async callback fired the moment a NEW WebSocket client
        attaches. Used by MonitorService to re-emit a RemoteAccessAlert with
        BrowserTabs once the extension is finally available (extensions take
        seconds to attach after agent startup; a startup-time RA alert is
        sent before that, with empty BrowserTabs)."""
        self._on_client_connect_callback = callback

    async def _notify_client_connected(self):
        """Run the optional connect hook without blocking message receipt."""
        try:
            await self._on_client_connect_callback()
        except Exception as e:
            logger.warning(f"on_client_connect callback failed: {e}")

    async def _handle_client(self, websocket: WebSocketServerProtocol):
        """Handle a client connection"""
        self.clients.add(websocket)
        client_id = id(websocket)
        connect_callback_task: Optional[asyncio.Task] = None
        logger.info(f"Extension connected (client {client_id})")
        print(f"\n[EXTENSION] Client connected (ID: {client_id})")

        # The callback may query tabs and wait for browser_tabs_response.
        # Schedule it independently so this handler enters the receive loop
        # immediately and can resolve the callback's pending Future.
        if self._on_client_connect_callback:
            connect_callback_task = asyncio.create_task(
                self._notify_client_connected()
            )
        
        try:
            async for message in websocket:
                try:
                    data = json.loads(message)
                    msg_type = data.get('type', 'unknown')

                    # Handle heartbeat directly (no need to forward, keeps logs clean)
                    if msg_type == 'heartbeat_ping':
                        await websocket.send(json.dumps({'type': 'heartbeat_pong'}))
                        logger.debug("Heartbeat pong sent")
                        continue

                    # Handle keepalive silently (no callback, just ACK received)
                    if msg_type == 'keepalive':
                        logger.debug("Keepalive received")
                        continue

                    # Handle browser tabs response — resolve the matching pending Future
                    if msg_type == 'browser_tabs_response':
                        request_id = data.get('requestId', '')
                        tabs = data.get('tabs', [])
                        key = f"{request_id}:{id(websocket)}"
                        future = self._tab_request_futures.get(key)
                        print(f"[EXT-SERVER][TABS-RESP] from client {id(websocket)} req={request_id[:8]}… "
                              f"tabs={len(tabs)} future_found={future is not None} "
                              f"future_done={future.done() if future else 'n/a'}")
                        if future and not future.done():
                            future.set_result(tabs)
                        continue  # Do not pass to regular callback

                    # Print ALL other messages - full visibility
                    # print("\n" + "=" * 70)
                    # print(f"<<< RECEIVED FROM EXTENSION [{msg_type}]")
                    # print("=" * 70)
                    # print(json.dumps(data, indent=2, ensure_ascii=False))
                    print("=" * 70)

                    logger.debug(f"Received from extension: {msg_type}")

                    if self._on_message_callback:
                        # Call the callback and get response
                        response = await self._on_message_callback(data)

                        if response:
                            await websocket.send(json.dumps(response))

                            # Print ALL responses - full visibility
                            response_type = response.get('type', 'response')
                            # print("\n" + "=" * 70)
                            # print(f">>> SENT TO EXTENSION [{response_type}]")
                            # print("=" * 70)
                            # print(json.dumps(response, indent=2, ensure_ascii=False))
                            # print("=" * 70 + "\n")

                            logger.debug(f"Sent to extension: {response_type}")
                            
                except json.JSONDecodeError as e:
                    logger.error(f"Invalid JSON from extension: {e}")
                except Exception as e:
                    logger.error(f"Error handling extension message: {e}")
                    
        except websockets.exceptions.ConnectionClosed:
            logger.info(f"Extension disconnected (client {client_id})")
            print(f"\n[EXTENSION] Client disconnected (ID: {client_id})")
        finally:
            self.clients.discard(websocket)
            if connect_callback_task and not connect_callback_task.done():
                connect_callback_task.cancel()
                try:
                    await connect_callback_task
                except asyncio.CancelledError:
                    pass
    
    async def _try_port(self, port: int) -> bool:
        """Try to start server on a specific port"""
        print(f"[EXT-SERVER] Trying port {port}...")
        try:
            self.server = await websockets.serve(
                self._handle_client,
                "localhost",
                port
            )
            self.port = port
            print(f"[EXT-SERVER] Successfully started on port {port}")
            logger.info(f"Extension server started on port {port}")
            return True
        except OSError as e:
            if "address already in use" in str(e).lower() or e.errno == 10048:
                print(f"[EXT-SERVER] Port {port} is busy, trying next...")
                logger.debug(f"Port {port} is busy")
                return False
            print(f"[EXT-SERVER] Error on port {port}: {e}")
            raise
    
    async def start(self) -> bool:
        """Start the server, trying multiple ports"""
        for port in EXTENSION_PORTS:
            if await self._try_port(port):
                self._running = True
                return True
                
        logger.error("Could not start extension server - all ports busy")
        return False
    
    async def stop(self):
        """Stop the server"""
        self._running = False
        
        # Close all client connections
        for client in self.clients.copy():
            try:
                await client.close()
            except Exception:
                logger.exception("Error closing WebSocket connection")
        self.clients.clear()
        
        # Close server
        if self.server:
            self.server.close()
            await self.server.wait_closed()
            self.server = None
            
        logger.info("Extension server stopped")
    
    async def request_browser_tabs(self, timeout: float = 3.0) -> List[dict]:
        """
        Ask all connected extensions for their open browser tabs.
        Sends a 'get_browser_tabs' message to every client and collects
        'browser_tabs_response' replies, merging them into a single list.
        Returns [] if no extensions are connected or none respond in time.
        """
        if not self.clients:
            return []

        request_id = str(uuid.uuid4())
        loop = asyncio.get_event_loop()
        futures: List[Tuple[asyncio.Future, str]] = []
        sent_futures: List[asyncio.Future] = []

        try:
            for client in list(self.clients):
                key = f"{request_id}:{id(client)}"
                future: asyncio.Future = loop.create_future()
                self._tab_request_futures[key] = future
                futures.append((future, key))
                try:
                    await client.send(json.dumps({
                        'type':      'get_browser_tabs',
                        'requestId': request_id
                    }))
                    sent_futures.append(future)
                except Exception as e:
                    logger.error(f"[TABS] Error sending tab request to client: {e}")
                    future.cancel()

            if not sent_futures:
                return []

            all_tabs: List[dict] = []
            done, _ = await asyncio.wait(sent_futures, timeout=timeout)

            for future in done:
                try:
                    all_tabs.extend(future.result() or [])
                except Exception:
                    pass

            logger.info(
                f"[TABS] Collected {len(all_tabs)} tabs "
                f"from {len(done)} extension(s)"
            )
            return all_tabs
        finally:
            # Cancellation can occur while sending or while asyncio.wait is
            # suspended. Always remove request IDs and cancel unresolved
            # Futures so a disconnect cannot leak per-client request state.
            for future, key in futures:
                if not future.done():
                    future.cancel()
                self._tab_request_futures.pop(key, None)

    async def broadcast(self, message: dict):
        """Send message to all connected extensions"""
        if not self.clients:
            return
            
        message_json = json.dumps(message)
        
        # Send to all clients
        disconnected = set()
        for client in self.clients:
            try:
                await client.send(message_json)
            except websockets.exceptions.ConnectionClosed:
                disconnected.add(client)
            except Exception as e:
                logger.error(f"Error broadcasting to client: {e}")
                disconnected.add(client)
        
        # Remove disconnected clients
        self.clients -= disconnected
    
    @property
    def is_running(self) -> bool:
        return self._running
    
    @property
    def client_count(self) -> int:
        return len(self.clients)


# For standalone testing
if __name__ == "__main__":
    logging.basicConfig(level=logging.DEBUG)
    
    server = ExtensionServer()
    
    async def handle_message(data):
        print(f"\nReceived: {json.dumps(data, indent=2)}")
        return {"status": "ok", "received": data.get('type')}
    
    server.on_message(handle_message)
    
    async def main():
        if await server.start():
            print(f"\nServer running on ws://localhost:{server.port}")
            print("Press Ctrl+C to stop\n")
            
            try:
                while True:
                    await asyncio.sleep(1)
            except KeyboardInterrupt:
                pass
            finally:
                await server.stop()
        else:
            print("Failed to start server")
    
    asyncio.run(main())
