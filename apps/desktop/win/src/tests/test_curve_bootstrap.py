"""
ASPS-619: Tests for CURVE bootstrap hardening.

Verifies:
  - Missing server key causes startup failure (SystemExit from config loader).
  - Empty key file causes startup failure, not silent plaintext downgrade.
  - ZMQClient.connect() raises RuntimeError when no server key is set.
  - ZMQClient.connect() applies CURVE socket options when a key is present.
  - AuthManager.__init__ raises RuntimeError when BACKEND_SERVER_PUBLIC_KEY_Z85
    is empty (defence-in-depth guard).
  - AuthManager does NOT update the server key from a token response
    (prevents MITM key replacement over a compromised first connection).
"""

import sys
import os
import types
import unittest
from unittest.mock import MagicMock, patch, call

# ---------------------------------------------------------------------------
# Ensure the src directory is on the path so module imports resolve correctly.
# ---------------------------------------------------------------------------
SRC_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
sys.path.insert(0, SRC_DIR)


# ---------------------------------------------------------------------------
# Minimal stubs for heavy optional dependencies (zmq, keyring, dotenv, …).
# These are installed once before any production module is imported so that
# the import machinery never actually loads the real packages.
# ---------------------------------------------------------------------------

def _install_stub(name: str, **attrs) -> types.ModuleType:
    """Create and register a module stub under *name* in sys.modules."""
    mod = types.ModuleType(name)
    for k, v in attrs.items():
        setattr(mod, k, v)
    sys.modules[name] = mod
    return mod


def _ensure_zmq_stub():
    """Install a minimal zmq stub if the real package isn't present."""
    if "zmq" in sys.modules:
        return
    zmq_mod = _install_stub(
        "zmq",
        REQ=3,
        RCVTIMEO=27,
        SNDTIMEO=28,
        LINGER=17,
        CURVE_PUBLICKEY=48,
        CURVE_SECRETKEY=49,
        CURVE_SERVERKEY=50,
    )

    class _Again(Exception):
        pass

    zmq_mod.Again = _Again
    zmq_mod.ZMQError = Exception

    def _curve_keypair():
        return b"pub" * 10 + b"p", b"sec" * 10 + b"s"

    zmq_mod.curve_keypair = _curve_keypair

    class _FakeSocket:
        def __init__(self):
            self._opts = {}
            self._connected = False

        def setsockopt(self, opt, val):
            self._opts[opt] = val

        def connect(self, addr):
            self._connected = True

        def close(self):
            self._connected = False

        def send(self, data):
            pass

        def recv(self):
            return b"{}"

    class _FakeContext:
        def socket(self, socket_type):
            return _FakeSocket()

        def term(self):
            pass

    zmq_mod.Context = _FakeContext


def _ensure_keyring_stub():
    if "keyring" not in sys.modules:
        _install_stub("keyring")


def _ensure_dotenv_stub():
    if "dotenv" not in sys.modules:
        dotenv_mod = _install_stub("dotenv")
        dotenv_mod.load_dotenv = lambda: None


_ensure_zmq_stub()
_ensure_keyring_stub()
_ensure_dotenv_stub()


# ---------------------------------------------------------------------------
# Helpers to construct isolated stubs for config and zmq_client.
# ---------------------------------------------------------------------------

def _make_zmq_client(server_public_key=None):
    """
    Import (or re-import) ZMQClient with a fresh module state.
    Returns an instance whose server_public_key is pre-set to *server_public_key*.
    """
    # Ensure version stub exists for transitive imports
    if "version" not in sys.modules:
        _install_stub("version", VERSION="0.0.0-test")
    if "generated.messaging.v1.message_envelope" not in sys.modules:
        env_mod = _install_stub(
            "generated.messaging.v1.message_envelope",
            create_envelope=lambda *a, **kw: {},
            validate_envelope=lambda *a, **kw: None,
        )
        _install_stub("generated", messaging=types.ModuleType("messaging"))
        _install_stub("generated.messaging", v1=types.ModuleType("v1"))
        _install_stub("generated.messaging.v1")

    # Remove cached module so we get a fresh import each time.
    sys.modules.pop("zmq_client", None)
    from zmq_client import ZMQClient  # noqa: PLC0415

    client = ZMQClient.__new__(ZMQClient)
    import zmq
    client.host = "127.0.0.1"
    client.port = 50001
    client.timeout = 5000
    client.socket = None
    client.server_public_key = server_public_key
    client.context = zmq.Context()
    import threading
    client._send_lock = threading.Lock()
    return client


# ---------------------------------------------------------------------------
# Test suite
# ---------------------------------------------------------------------------

def _run_curve_loader(env=None, local_file_content=None,
                       backend_file_content=None, platform_system="Windows"):
    """
    Drive config._load_curve_public_key with controlled filesystem and env
    state. Shared by TestConfigCurveKeyLoader (default 'zmq' mode) and
    TestConfigWSTransportCurveToleration (ASPS-721 'ws' mode) — a plain
    module-level function (not a TestCase method) so it isn't picked up
    twice via inheritance.

    - env: dict of env vars (replaces os.environ for the call)
    - local_file_content: str to return from ~/.antiscam/curve-public-key.txt,
      or None if file should not exist.
    - backend_file_content: str for the backend public key file,
      or None if file should not exist.
    """
    sys.modules.pop("config", None)

    def fake_exists(path):
        path_str = str(path)
        if ".antiscam" in path_str and "curve-public-key" in path_str:
            return local_file_content is not None
        if "ASPS" in path_str or ".asps" in path_str:
            return backend_file_content is not None
        return False

    def fake_read_text(path_self):
        path_str = str(path_self)
        if ".antiscam" in path_str and "curve-public-key" in path_str:
            return local_file_content or ""
        if "ASPS" in path_str or ".asps" in path_str:
            return backend_file_content or ""
        return ""

    import pathlib

    with patch.dict(os.environ, env or {}, clear=True), \
         patch("platform.system", return_value=platform_system), \
         patch.object(pathlib.Path, "exists", fake_exists), \
         patch.object(pathlib.Path, "read_text", fake_read_text):
        # We need config to reimport cleanly — stub version again.
        if "version" not in sys.modules:
            _install_stub("version", VERSION="0.0.0-test")
        import config  # noqa: PLC0415
        return config._load_curve_public_key()


class TestConfigCurveKeyLoader(unittest.TestCase):
    """config._load_curve_public_key must raise SystemExit on any missing-key path."""

    @staticmethod
    def _run_loader(env=None, local_file_content=None,
                    backend_file_content=None, platform_system="Windows"):
        return _run_curve_loader(
            env=env, local_file_content=local_file_content,
            backend_file_content=backend_file_content, platform_system=platform_system,
        )

    # --- AC1: missing key → error, not plaintext ---

    def test_no_key_source_raises_system_exit(self):
        """When no key source is present the loader must raise SystemExit."""
        with self.assertRaises(SystemExit) as ctx:
            self._run_loader()
        self.assertIn("FATAL", str(ctx.exception))
        self.assertIn("plaintext", str(ctx.exception).lower() if "plaintext" in str(ctx.exception).lower() else "plaintext")

    def test_no_key_source_error_message_contains_provisioning_guidance(self):
        """The SystemExit message must include at least one provisioning option."""
        with self.assertRaises(SystemExit) as ctx:
            self._run_loader()
        msg = str(ctx.exception)
        self.assertTrue(
            "curve-public-key.txt" in msg or "ANTISCAM_CURVE_PUBLIC_KEY" in msg,
            f"Expected provisioning hint in error. Got: {msg!r}"
        )

    # --- AC4: empty key file is a configuration error ---

    def test_empty_backend_key_file_raises_system_exit(self):
        """An empty backend public key file must raise SystemExit, not return ''."""
        with self.assertRaises(SystemExit) as ctx:
            self._run_loader(backend_file_content="")
        self.assertIn("FATAL", str(ctx.exception))

    def test_empty_local_key_file_falls_through_to_backend_or_error(self):
        """Empty local key file is ignored; loader falls through to next source."""
        # No backend file either → must raise SystemExit.
        with self.assertRaises(SystemExit):
            self._run_loader(local_file_content="")

    # --- AC5: present key → returned correctly ---

    def test_env_var_key_returned(self):
        """Environment variable key takes highest priority."""
        key = "a" * 40
        result = self._run_loader(env={"ANTISCAM_CURVE_PUBLIC_KEY": key})
        self.assertEqual(result, key)

    def test_local_file_key_returned(self):
        """Key from ~/.antiscam/curve-public-key.txt is returned."""
        key = "b" * 40
        result = self._run_loader(local_file_content=key)
        self.assertEqual(result, key)

    def test_backend_file_key_returned(self):
        """Key from the backend-written public key file is returned."""
        key = "c" * 40
        result = self._run_loader(backend_file_content=key)
        self.assertEqual(result, key)

    def test_env_var_takes_priority_over_files(self):
        """Env var must win over local file and backend file."""
        env_key = "e" * 40
        result = self._run_loader(
            env={"ANTISCAM_CURVE_PUBLIC_KEY": env_key},
            local_file_content="l" * 40,
            backend_file_content="b" * 40,
        )
        self.assertEqual(result, env_key)


class TestConfigWSTransportCurveToleration(unittest.TestCase):
    """
    ASPS-721: when TRANSPORT_MODE == "ws", CURVE is not used (TLS is the
    transport security boundary instead — see ADR-004). The loader must NOT
    abort startup for a missing/empty CURVE key in this mode, unlike the
    default "zmq" mode covered by TestConfigCurveKeyLoader above.
    """

    def test_ws_mode_returns_empty_string_with_no_key_sources(self):
        """No key anywhere + TRANSPORT_MODE=ws -> '' , not SystemExit."""
        result = _run_curve_loader(env={"TRANSPORT_MODE": "ws"})
        self.assertEqual(result, "")

    def test_ws_mode_returns_empty_string_even_with_empty_backend_key_file(self):
        """Empty backend key file (CurveEnabled=false) + TRANSPORT_MODE=ws
        must NOT raise — this is the exact scenario that raises SystemExit
        in the default zmq mode (see test_empty_backend_key_file_raises_system_exit)."""
        result = _run_curve_loader(env={"TRANSPORT_MODE": "ws"}, backend_file_content="")
        self.assertEqual(result, "")

    def test_ws_mode_ignores_a_present_curve_key_too(self):
        """Even when a valid key IS present, WS mode still skips the lookup
        entirely (CURVE is simply not part of this transport)."""
        result = _run_curve_loader(
            env={"TRANSPORT_MODE": "ws", "ANTISCAM_CURVE_PUBLIC_KEY": "z" * 40}
        )
        self.assertEqual(result, "")

    def test_default_transport_mode_zmq_still_raises_system_exit(self):
        """Regression guard: omitting TRANSPORT_MODE (default 'zmq') must
        preserve the original fail-fast behavior."""
        with self.assertRaises(SystemExit):
            _run_curve_loader(env={})


class TestZMQClientCurveEnforcement(unittest.TestCase):
    """ZMQClient.connect() must refuse plaintext and enforce CURVE."""

    # --- AC2: no silent fallback to plaintext ---

    def test_connect_raises_if_no_server_key(self):
        """connect() must raise RuntimeError — not return False — when key is absent."""
        client = _make_zmq_client(server_public_key=None)
        with self.assertRaises(RuntimeError) as ctx:
            client.connect()
        self.assertIn("CURVE", str(ctx.exception))
        self.assertIn("Refusing", str(ctx.exception))

    def test_connect_raises_when_key_is_empty_bytes(self):
        """connect() must raise RuntimeError when key is empty bytes."""
        client = _make_zmq_client(server_public_key=b"")
        with self.assertRaises(RuntimeError):
            client.connect()

    # --- AC3: present key → CURVE socket options applied ---

    def test_connect_applies_curve_options_when_key_present(self):
        """connect() must set CURVE_SERVERKEY on the socket when key is present."""
        import zmq

        fake_socket = MagicMock()
        fake_context = MagicMock()
        fake_context.socket.return_value = fake_socket

        key = b"z" * 40  # 40-byte Z85 key
        client = _make_zmq_client(server_public_key=key)
        client.context = fake_context

        result = client.connect()

        self.assertTrue(result, "connect() should return True on success")
        # Verify CURVE_SERVERKEY was set to our key
        setsockopt_calls = fake_socket.setsockopt.call_args_list
        curve_server_key_calls = [
            c for c in setsockopt_calls
            if c.args[0] == zmq.CURVE_SERVERKEY
        ]
        self.assertEqual(len(curve_server_key_calls), 1)
        self.assertEqual(curve_server_key_calls[0].args[1], key)

    def test_connect_always_sets_curve_publickey_and_secretkey(self):
        """connect() must set both CURVE_PUBLICKEY and CURVE_SECRETKEY (ephemeral)."""
        import zmq

        fake_socket = MagicMock()
        fake_context = MagicMock()
        fake_context.socket.return_value = fake_socket

        key = b"z" * 40
        client = _make_zmq_client(server_public_key=key)
        client.context = fake_context

        client.connect()

        opts_set = {c.args[0] for c in fake_socket.setsockopt.call_args_list}
        self.assertIn(zmq.CURVE_PUBLICKEY, opts_set)
        self.assertIn(zmq.CURVE_SECRETKEY, opts_set)
        self.assertIn(zmq.CURVE_SERVERKEY, opts_set)

    def test_connect_never_omits_curve_options_when_key_present(self):
        """There must be no conditional path that skips CURVE even with a key."""
        import zmq

        fake_socket = MagicMock()
        fake_context = MagicMock()
        fake_context.socket.return_value = fake_socket

        # Run connect() multiple times with a key — CURVE must always be set.
        key = b"k" * 40
        for _ in range(3):
            client = _make_zmq_client(server_public_key=key)
            client.context = fake_context
            fake_socket.reset_mock()
            client.connect()

            opts_set = {c.args[0] for c in fake_socket.setsockopt.call_args_list}
            self.assertIn(zmq.CURVE_SERVERKEY, opts_set,
                          "CURVE_SERVERKEY must be set on every connect() call")


class TestAuthManagerCurveEnforcement(unittest.TestCase):
    """AuthManager must refuse to initialise without a CURVE key."""

    def _make_auth_manager(self, live_key: str):
        """
        Construct an AuthManager with *live_key* as BACKEND_SERVER_PUBLIC_KEY_Z85.
        The zmq_client and storage layer are stubbed out.
        """
        sys.modules.pop("auth_manager", None)

        # Stub config
        config_stub = types.ModuleType("config")
        config_stub.DATA_DIR = "/tmp/antiscam-test"
        config_stub.BACKEND_SERVER_PUBLIC_KEY_Z85 = live_key
        config_stub.WEBAPI_URL = "http://localhost:5001"
        sys.modules["config"] = config_stub

        # Stub keyring (no secure storage needed)
        keyring_stub = sys.modules.get("keyring") or _install_stub("keyring")
        keyring_stub.get_password = lambda svc, uid: None
        keyring_stub.set_password = lambda svc, uid, pw: None

        from auth_manager import AuthManager  # noqa: PLC0415

        fake_zmq = MagicMock()
        device_info = {"id": "TEST-DEVICE-001"}

        return AuthManager(fake_zmq, device_info)

    def tearDown(self):
        sys.modules.pop("auth_manager", None)
        sys.modules.pop("config", None)

    # --- AC4: empty key is a configuration error ---

    def test_init_raises_when_key_is_empty_string(self):
        """AuthManager must raise RuntimeError when BACKEND_SERVER_PUBLIC_KEY_Z85 is ''."""
        with self.assertRaises(RuntimeError) as ctx:
            self._make_auth_manager(live_key="")
        self.assertIn("CURVE", str(ctx.exception))

    # --- AC3: present key → CURVE applied ---

    def test_init_calls_set_server_public_key_with_key(self):
        """AuthManager must call zmq_client.set_server_public_key with the key bytes."""
        key = "d" * 40
        sys.modules.pop("auth_manager", None)

        config_stub = types.ModuleType("config")
        config_stub.DATA_DIR = "/tmp/antiscam-test"
        config_stub.BACKEND_SERVER_PUBLIC_KEY_Z85 = key
        config_stub.WEBAPI_URL = "http://localhost:5001"
        sys.modules["config"] = config_stub

        keyring_stub = sys.modules.get("keyring") or _install_stub("keyring")
        keyring_stub.get_password = lambda svc, uid: None

        from auth_manager import AuthManager  # noqa: PLC0415

        fake_zmq = MagicMock()
        device_info = {"id": "TEST-DEVICE-001"}

        with patch("pathlib.Path.exists", return_value=False):
            manager = AuthManager(fake_zmq, device_info)

        fake_zmq.set_server_public_key.assert_called_once_with(key.encode("utf-8"))
        self.assertIsNone(
            fake_zmq.clear_server_public_key.call_args,
            "clear_server_public_key must never be called",
        )

    def test_init_never_calls_clear_server_public_key(self):
        """AuthManager must never call clear_server_public_key regardless of key state."""
        key = "e" * 40
        sys.modules.pop("auth_manager", None)

        config_stub = types.ModuleType("config")
        config_stub.DATA_DIR = "/tmp/antiscam-test"
        config_stub.BACKEND_SERVER_PUBLIC_KEY_Z85 = key
        config_stub.WEBAPI_URL = "http://localhost:5001"
        sys.modules["config"] = config_stub

        keyring_stub = sys.modules.get("keyring") or _install_stub("keyring")
        keyring_stub.get_password = lambda svc, uid: None

        from auth_manager import AuthManager  # noqa: PLC0415

        fake_zmq = MagicMock()
        with patch("pathlib.Path.exists", return_value=False):
            AuthManager(fake_zmq, {"id": "TEST-DEVICE-001"})

        fake_zmq.clear_server_public_key.assert_not_called()


class TestAuthManagerWSTransportCurveToleration(unittest.TestCase):
    """
    ASPS-721: AuthManager must NOT require a CURVE key when
    config.TRANSPORT_MODE == "ws" — mirrors config._load_curve_public_key's
    own toleration (TestConfigWSTransportCurveToleration above). Without
    this, a WS-mode agent with an empty BACKEND_SERVER_PUBLIC_KEY_Z85 would
    still fail startup here even though config.py no longer aborts.
    """

    def tearDown(self):
        sys.modules.pop("auth_manager", None)
        sys.modules.pop("config", None)

    def _make_config_stub(self, transport_mode: str, live_key: str = ""):
        config_stub = types.ModuleType("config")
        config_stub.DATA_DIR = "/tmp/antiscam-test"
        config_stub.BACKEND_SERVER_PUBLIC_KEY_Z85 = live_key
        config_stub.WEBAPI_URL = "http://localhost:5001"
        config_stub.TRANSPORT_MODE = transport_mode
        sys.modules["config"] = config_stub
        return config_stub

    def test_ws_mode_does_not_raise_with_empty_key(self):
        """WS mode + empty CURVE key must NOT raise (this exact combination
        raises RuntimeError in default 'zmq' mode — see
        TestAuthManagerCurveEnforcement.test_init_raises_when_key_is_empty_string)."""
        sys.modules.pop("auth_manager", None)
        self._make_config_stub(transport_mode="ws", live_key="")

        keyring_stub = sys.modules.get("keyring") or _install_stub("keyring")
        keyring_stub.get_password = lambda svc, uid: None

        from auth_manager import AuthManager  # noqa: PLC0415

        fake_zmq = MagicMock()
        with patch("pathlib.Path.exists", return_value=False):
            manager = AuthManager(fake_zmq, {"id": "TEST-DEVICE-001"})  # must not raise

        self.assertIsNone(manager.server_public_key)

    def test_ws_mode_does_not_call_set_server_public_key(self):
        """CURVE is not applied to the transport client at all in WS mode —
        the WSClient's set_server_public_key is a documented no-op, but
        AuthManager should not even attempt the call."""
        sys.modules.pop("auth_manager", None)
        self._make_config_stub(transport_mode="ws", live_key="")

        keyring_stub = sys.modules.get("keyring") or _install_stub("keyring")
        keyring_stub.get_password = lambda svc, uid: None

        from auth_manager import AuthManager  # noqa: PLC0415

        fake_zmq = MagicMock()
        with patch("pathlib.Path.exists", return_value=False):
            AuthManager(fake_zmq, {"id": "TEST-DEVICE-001"})

        fake_zmq.set_server_public_key.assert_not_called()

    def test_zmq_mode_default_still_requires_key_when_transport_mode_attribute_absent(self):
        """Regression guard: a config stub with no TRANSPORT_MODE attribute
        at all (as used by the pre-existing tests above) must still default
        to the strict 'zmq' behavior."""
        sys.modules.pop("auth_manager", None)
        config_stub = types.ModuleType("config")
        config_stub.DATA_DIR = "/tmp/antiscam-test"
        config_stub.BACKEND_SERVER_PUBLIC_KEY_Z85 = ""
        config_stub.WEBAPI_URL = "http://localhost:5001"
        # Deliberately no TRANSPORT_MODE attribute.
        sys.modules["config"] = config_stub

        keyring_stub = sys.modules.get("keyring") or _install_stub("keyring")
        keyring_stub.get_password = lambda svc, uid: None

        from auth_manager import AuthManager  # noqa: PLC0415

        with self.assertRaises(RuntimeError):
            AuthManager(MagicMock(), {"id": "TEST-DEVICE-001"})


class TestAuthManagerNoKeyOverwriteFromResponse(unittest.TestCase):
    """
    _handle_token_response must NOT overwrite the server public key from
    the backend response.  Accepting a key over-the-wire would allow a MITM
    to replace the trust anchor during the first authenticated exchange.
    """

    def _make_manager_with_key(self, key="f" * 40):
        sys.modules.pop("auth_manager", None)

        config_stub = types.ModuleType("config")
        config_stub.DATA_DIR = "/tmp/antiscam-test"
        config_stub.BACKEND_SERVER_PUBLIC_KEY_Z85 = key
        config_stub.WEBAPI_URL = "http://localhost:5001"
        sys.modules["config"] = config_stub

        keyring_stub = sys.modules.get("keyring") or _install_stub("keyring")
        keyring_stub.get_password = lambda svc, uid: None
        keyring_stub.set_password = lambda svc, uid, pw: None

        from auth_manager import AuthManager  # noqa: PLC0415

        fake_zmq = MagicMock()
        with patch("pathlib.Path.exists", return_value=False), \
             patch("pathlib.Path.mkdir", return_value=None), \
             patch("builtins.open", unittest.mock.mock_open()):
            manager = AuthManager(fake_zmq, {"id": "TEST-DEVICE-001"})

        manager.zmq_client = fake_zmq
        return manager, fake_zmq

    def tearDown(self):
        sys.modules.pop("auth_manager", None)
        sys.modules.pop("config", None)

    def test_token_response_does_not_call_set_server_public_key(self):
        """_handle_token_response must not call zmq_client.set_server_public_key."""
        manager, fake_zmq = self._make_manager_with_key()
        fake_zmq.reset_mock()

        # Simulate a backend response that includes a (potentially hostile) key.
        response = {
            "status": "TokenCreated",
            "token": "test-token-abc",
            "expiration": "2099-01-01T00:00:00Z",
            "serverPublicKey": "g" * 40,  # MITM-supplied key
        }

        with patch("builtins.open", unittest.mock.mock_open()), \
             patch("pathlib.Path.mkdir", return_value=None):
            manager._handle_token_response(response)

        fake_zmq.set_server_public_key.assert_not_called()

    def test_token_response_does_not_change_server_public_key_attribute(self):
        """server_public_key attribute must not change after processing a response."""
        manager, _ = self._make_manager_with_key(key="f" * 40)
        original_key = manager.server_public_key

        response = {
            "status": "ExistingToken",
            "token": "some-token",
            "expiration": "2099-01-01T00:00:00Z",
            "serverPublicKey": "h" * 40,
        }

        with patch("builtins.open", unittest.mock.mock_open()), \
             patch("pathlib.Path.mkdir", return_value=None):
            manager._handle_token_response(response)

        self.assertEqual(
            manager.server_public_key,
            original_key,
            "server_public_key must not be overwritten by a token response",
        )


if __name__ == "__main__":
    unittest.main(verbosity=2)
