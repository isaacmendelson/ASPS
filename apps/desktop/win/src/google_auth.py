"""
AntiScam Desktop App - Google Authentication
Handles Google OAuth2 sign-in for desktop application
"""

import json
import os
import webbrowser
import http.server
import socketserver
import urllib.parse
import threading
import logging
from pathlib import Path
from typing import Optional, Callable
import requests

from config import GOOGLE_CLIENT_ID, GOOGLE_CLIENT_SECRET

logger = logging.getLogger(__name__)

# Google OAuth2 Configuration
GOOGLE_AUTH_URL = "https://accounts.google.com/o/oauth2/auth"
GOOGLE_TOKEN_URL = "https://oauth2.googleapis.com/token"
GOOGLE_USERINFO_URL = "https://www.googleapis.com/oauth2/v2/userinfo"
REDIRECT_PORT = 8912
REDIRECT_URI = f"http://localhost:{REDIRECT_PORT}"
SCOPES = "email profile"


class GoogleAuth:
    """
    Google OAuth2 authentication for desktop app.
    
    Flow:
    1. Open browser to Google sign-in
    2. User signs in
    3. Google redirects to localhost
    4. We capture the code
    5. Exchange code for tokens
    6. Get user email
    """
    
    def __init__(self, data_dir: str = "~/.antiscam"):
        self.data_dir = Path(os.path.expanduser(data_dir))
        self.token_file = self.data_dir / "google_token.json"
        
        # User info
        self.email: Optional[str] = None
        self.name: Optional[str] = None
        self.picture: Optional[str] = None
        self.access_token: Optional[str] = None
        self.refresh_token: Optional[str] = None
        
        # Callback
        self._on_signed_in: Optional[Callable] = None
        
        # Auth code from redirect
        self._auth_code: Optional[str] = None
        self._auth_event = threading.Event()
        
        print("[GOOGLE] Auth module initialized")
        
        # Try to load saved token
        self._load_token()
    
    def on_signed_in(self, callback: Callable):
        """Set callback for when sign-in completes"""
        self._on_signed_in = callback
    
    def _load_token(self):
        """Load saved token from disk"""
        try:
            if self.token_file.exists():
                with open(self.token_file, 'r') as f:
                    data = json.load(f)
                
                self.email = data.get('email')
                self.name = data.get('name')
                self.picture = data.get('picture')
                self.access_token = data.get('access_token')
                self.refresh_token = data.get('refresh_token')
                
                if self.email:
                    print(f"[GOOGLE] Loaded saved user: {self.email}")
                    return True
        except Exception as e:
            print(f"[GOOGLE] Error loading token: {e}")
        
        return False
    
    def _save_token(self):
        """Save token to disk"""
        try:
            self.data_dir.mkdir(parents=True, exist_ok=True)
            
            data = {
                'email': self.email,
                'name': self.name,
                'picture': self.picture,
                'access_token': self.access_token,
                'refresh_token': self.refresh_token
            }
            
            with open(self.token_file, 'w') as f:
                json.dump(data, f, indent=2)
            
            print(f"[GOOGLE] Token saved")
        except Exception as e:
            print(f"[GOOGLE] Error saving token: {e}")
    
    def is_signed_in(self) -> bool:
        """Check if user is signed in"""
        return self.email is not None and self.access_token is not None
    
    def get_email(self) -> Optional[str]:
        """Get signed-in user's email"""
        return self.email
    
    def sign_in(self) -> bool:
        """
        Start Google sign-in flow.
        Opens browser and waits for user to sign in.
        Returns True if successful.
        """
        print("\n" + "=" * 60)
        print("[GOOGLE] STARTING SIGN-IN")
        print("=" * 60)
        
        # Build auth URL
        params = {
            'client_id': GOOGLE_CLIENT_ID,
            'redirect_uri': REDIRECT_URI,
            'response_type': 'code',
            'scope': SCOPES,
            'access_type': 'offline',
            'prompt': 'consent'
        }
        
        auth_url = f"{GOOGLE_AUTH_URL}?{urllib.parse.urlencode(params)}"
        
        print(f"[GOOGLE] Opening browser for sign-in...")
        print(f"[GOOGLE] If browser doesn't open, go to:")
        print(f"[GOOGLE] {auth_url[:80]}...")
        
        # Start local server to catch redirect
        self._auth_code = None
        self._auth_event.clear()
        
        server_thread = threading.Thread(target=self._run_redirect_server, daemon=True)
        server_thread.start()
        
        # Open browser
        webbrowser.open(auth_url)
        
        # Wait for redirect (max 120 seconds)
        print("[GOOGLE] Waiting for sign-in...")
        if not self._auth_event.wait(timeout=120):
            print("[GOOGLE] Sign-in timeout!")
            return False
        
        if not self._auth_code:
            print("[GOOGLE] No auth code received!")
            return False
        
        print("[GOOGLE] Got auth code, exchanging for token...")
        
        # Exchange code for token
        if not self._exchange_code():
            return False
        
        # Get user info
        if not self._get_user_info():
            return False
        
        # Save token
        self._save_token()
        
        print(f"\n[GOOGLE] ✅ Signed in as: {self.email}")
        print("=" * 60 + "\n")
        
        # Call callback
        if self._on_signed_in:
            self._on_signed_in(self.email)
        
        return True
    
    def _run_redirect_server(self):
        """Run local server to catch OAuth redirect"""
        
        parent = self
        
        class RedirectHandler(http.server.BaseHTTPRequestHandler):
            def do_GET(self):
                # Parse query string
                query = urllib.parse.urlparse(self.path).query
                params = urllib.parse.parse_qs(query)
                
                if 'code' in params:
                    parent._auth_code = params['code'][0]
                    
                    # Send success page
                    self.send_response(200)
                    self.send_header('Content-type', 'text/html')
                    self.end_headers()
                    
                    html = """
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <title>AntiScam - Signed In</title>
                        <style>
                            body {
                                font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                                background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
                                color: white;
                                display: flex;
                                justify-content: center;
                                align-items: center;
                                height: 100vh;
                                margin: 0;
                            }
                            .container {
                                text-align: center;
                                padding: 40px;
                                background: rgba(255,255,255,0.1);
                                border-radius: 20px;
                            }
                            .icon { font-size: 64px; margin-bottom: 20px; }
                            h1 { margin: 0 0 10px 0; }
                            p { color: #888; }
                        </style>
                    </head>
                    <body>
                        <div class="container">
                            <div class="icon">✅</div>
                            <h1>Signed In Successfully!</h1>
                            <p>You can close this window and return to AntiScam.</p>
                        </div>
                    </body>
                    </html>
                    """
                    self.wfile.write(html.encode())
                else:
                    # Error
                    self.send_response(400)
                    self.send_header('Content-type', 'text/html')
                    self.end_headers()
                    self.wfile.write(b"<h1>Error: No authorization code</h1>")
                
                # Signal that we got the code
                parent._auth_event.set()
            
            def log_message(self, format, *args):
                pass  # Suppress logging
        
        try:
            with socketserver.TCPServer(("", REDIRECT_PORT), RedirectHandler) as server:
                server.timeout = 120
                server.handle_request()
        except Exception as e:
            print(f"[GOOGLE] Redirect server error: {e}")
            self._auth_event.set()
    
    def _exchange_code(self) -> bool:
        """Exchange auth code for access token"""
        try:
            response = requests.post(GOOGLE_TOKEN_URL, data={
                'code': self._auth_code,
                'client_id': GOOGLE_CLIENT_ID,
                'client_secret': GOOGLE_CLIENT_SECRET,
                'redirect_uri': REDIRECT_URI,
                'grant_type': 'authorization_code'
            })
            
            if response.status_code != 200:
                print(f"[GOOGLE] Token exchange failed: {response.text}")
                return False
            
            data = response.json()
            self.access_token = data.get('access_token')
            self.refresh_token = data.get('refresh_token')
            
            print("[GOOGLE] Token exchange successful")
            return True
            
        except Exception as e:
            print(f"[GOOGLE] Token exchange error: {e}")
            return False
    
    def _get_user_info(self) -> bool:
        """Get user info from Google"""
        try:
            response = requests.get(
                GOOGLE_USERINFO_URL,
                headers={'Authorization': f'Bearer {self.access_token}'}
            )
            
            if response.status_code != 200:
                print(f"[GOOGLE] Failed to get user info: {response.text}")
                return False
            
            data = response.json()
            self.email = data.get('email')
            self.name = data.get('name')
            self.picture = data.get('picture')
            
            print(f"[GOOGLE] User: {self.name} ({self.email})")
            return True
            
        except Exception as e:
            print(f"[GOOGLE] Error getting user info: {e}")
            return False
    
    def sign_out(self):
        """Sign out and clear saved data"""
        print("[GOOGLE] Signing out...")
        
        self.email = None
        self.name = None
        self.picture = None
        self.access_token = None
        self.refresh_token = None
        
        try:
            if self.token_file.exists():
                os.remove(self.token_file)
        except Exception as e:
            print(f"[GOOGLE] Error removing token file: {e}")
        
        print("[GOOGLE] Signed out")
    
    def refresh_access_token(self) -> bool:
        """Refresh the access token"""
        if not self.refresh_token:
            return False
        
        try:
            response = requests.post(GOOGLE_TOKEN_URL, data={
                'client_id': GOOGLE_CLIENT_ID,
                'client_secret': GOOGLE_CLIENT_SECRET,
                'refresh_token': self.refresh_token,
                'grant_type': 'refresh_token'
            })
            
            if response.status_code != 200:
                print(f"[GOOGLE] Token refresh failed: {response.text}")
                return False
            
            data = response.json()
            self.access_token = data.get('access_token')
            
            self._save_token()
            print("[GOOGLE] Token refreshed")
            return True
            
        except Exception as e:
            print(f"[GOOGLE] Token refresh error: {e}")
            return False


# Standalone test
if __name__ == "__main__":
    logging.basicConfig(level=logging.DEBUG)
    
    print("=" * 60)
    print("GOOGLE AUTH - STANDALONE TEST")
    print("=" * 60)
    
    auth = GoogleAuth()
    
    if auth.is_signed_in():
        print(f"\nAlready signed in as: {auth.email}")
        choice = input("Sign out? (y/n): ")
        if choice.lower() == 'y':
            auth.sign_out()
    
    if not auth.is_signed_in():
        print("\nStarting sign-in...")
        if auth.sign_in():
            print(f"\n✅ Success! Email: {auth.email}")
        else:
            print("\n❌ Sign-in failed")
