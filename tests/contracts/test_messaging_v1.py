import copy
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
FIXTURES = ROOT / "contracts/messaging/v1/fixtures"
PY_BINDING = ROOT / "apps/desktop/win/src/generated/messaging/v1/message_envelope.py"
ANALYZER_BINDING = ROOT / "Analyzers/basic-url-analyzer/generated/messaging/v1/message_envelope.py"
JS_BINDING = ROOT / "apps/extension/chrome/generated/messaging/v1/message-envelope.js"

spec = importlib.util.spec_from_file_location("message_envelope_v1", PY_BINDING)
binding = importlib.util.module_from_spec(spec)
spec.loader.exec_module(binding)
analyzer_spec = importlib.util.spec_from_file_location("analyzer_message_envelope_v1", ANALYZER_BINDING)
analyzer_binding = importlib.util.module_from_spec(analyzer_spec)
analyzer_spec.loader.exec_module(analyzer_binding)

class MessagingV1ContractTests(unittest.TestCase):
    def fixtures(self):
        for path in sorted(FIXTURES.glob("*.json")):
            yield path, json.loads(path.read_text(encoding="utf-8"))

    def test_all_golden_fixtures_validate_in_python_and_javascript(self):
        for path, value in self.fixtures():
            with self.subTest(path=path.name):
                binding.validate_envelope(value)
                analyzer_binding.validate_envelope(value)
                script = (
                    "const {pathToFileURL}=require('node:url');"
                    f"(async()=>{{await import(pathToFileURL({json.dumps(str(JS_BINDING))}));"
                    f"globalThis.AspsMessagingV1.validateEnvelope({json.dumps(value)});}})();"
                )
                subprocess.run(["node", "-e", script], check=True, capture_output=True, text=True)

    def test_unsupported_version_fails_explicitly_in_both_languages(self):
        _, value = next(self.fixtures())
        value["schemaVersion"] = "2.0"
        with self.assertRaises(binding.ContractError) as caught:
            binding.validate_envelope(value)
        self.assertEqual("protocol.unsupported_schema_version", caught.exception.code)
        script = (
            "const {pathToFileURL}=require('node:url');"
            f"(async()=>{{await import(pathToFileURL({json.dumps(str(JS_BINDING))}));"
            f"try{{globalThis.AspsMessagingV1.validateEnvelope({json.dumps(value)})}}"
            "catch(e){process.stdout.write(e.code)}})();"
        )
        result = subprocess.run(["node", "-e", script], check=True, capture_output=True, text=True)
        self.assertEqual("protocol.unsupported_schema_version", result.stdout)

    def test_additive_minor_version_is_accepted_by_all_generated_consumers(self):
        _, value = next(self.fixtures())
        value["schemaVersion"] = "1.9"
        binding.validate_envelope(value)
        analyzer_binding.validate_envelope(value)
        script = (
            "const {pathToFileURL}=require('node:url');"
            f"(async()=>{{await import(pathToFileURL({json.dumps(str(JS_BINDING))}));"
            f"globalThis.AspsMessagingV1.validateEnvelope({json.dumps(value)});}})();"
        )
        subprocess.run(["node", "-e", script], check=True, capture_output=True, text=True)

    def test_every_missing_required_json_field_fails(self):
        _, value = next(self.fixtures())
        for field in tuple(value):
            with self.subTest(field=field):
                invalid = copy.deepcopy(value)
                invalid.pop(field)
                with self.assertRaises(binding.ContractError) as caught:
                    binding.validate_envelope(invalid)
                self.assertEqual("protocol.malformed_envelope", caught.exception.code)

    def test_concurrent_same_url_out_of_order_resolves_by_request_id(self):
        tracker = binding.RequestTracker()
        first = {"requestId": "7afe6cba-7916-40e1-91dc-666f40f760db", "correlationId": "6b7a9fa7-e3f0-4c5c-86cc-3914f42b262f", "context": {"deviceId": "d", "tabId": "1", "url": "https://example.com/"}}
        second = {"requestId": "eeb790bc-e494-464c-9bbb-a74040479fd1", "correlationId": "c8dc8f4c-ca73-4aa6-9eaf-d4ca5b9fb65f", "context": {"deviceId": "d", "tabId": "2", "url": "https://example.com/"}}
        tracker.add(first)
        tracker.add(second)
        self.assertEqual(second, tracker.resolve(second))
        self.assertEqual(first, tracker.resolve(first))

    def test_out_of_order_stale_context_is_rejected(self):
        tracker = binding.RequestTracker()
        request = {"requestId": "7afe6cba-7916-40e1-91dc-666f40f760db", "correlationId": "6b7a9fa7-e3f0-4c5c-86cc-3914f42b262f", "context": {"deviceId": "d", "tabId": "1", "url": "https://example.com/first"}}
        tracker.add(request)
        stale = copy.deepcopy(request)
        stale["context"]["url"] = "https://example.com/second"
        with self.assertRaises(binding.ContractError) as caught:
            tracker.resolve(stale)
        self.assertEqual("validation.immutable_context_mismatch", caught.exception.code)

if __name__ == "__main__":
    unittest.main()
