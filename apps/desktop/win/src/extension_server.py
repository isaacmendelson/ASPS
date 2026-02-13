"""
AntiScam Desktop App - Extension Server
WebSocket server for Chrome extension communication
"""

import asyncio
import json
import logging
from typing import Optional, Callable, Set
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
        self._running = False
        
    def on_message(self, callback: Callable):
        """Set callback for incoming messages"""
        self._on_message_callback = callback
        
    async def _handle_client(self, websocket: WebSocketServerProtocol):
        """Handle a client connection"""
        self.clients.add(websocket)
        client_id = id(websocket)
        logger.info(f"Extension connected (client {client_id})")
        print(f"\n[EXTENSION] Client connected (ID: {client_id})")
        
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

                    # Print ALL other messages - full visibility
                    print("\n" + "=" * 70)
                    print(f"<<< RECEIVED FROM EXTENSION [{msg_type}]")
                    print("=" * 70)
                    print(json.dumps(data, indent=2, ensure_ascii=False))
                    print("=" * 70)

                    logger.debug(f"Received from extension: {msg_type}")

                    if self._on_message_callback:
                        # Call the callback and get response
                        response = await self._on_message_callback(data)

                        if response:
                            await websocket.send(json.dumps(response))

                            # Print ALL responses - full visibility
                            response_type = response.get('type', 'response')
                            print("\n" + "=" * 70)
                            print(f">>> SENT TO EXTENSION [{response_type}]")
                            print("=" * 70)
                            print(json.dumps(response, indent=2, ensure_ascii=False))
                            print("=" * 70 + "\n")

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
