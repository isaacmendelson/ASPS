#!/usr/bin/env python3
"""Generate and drift-check ASPS messaging v1 bindings."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
CONTRACT = ROOT / "contracts/messaging/v1"
SCHEMAS = tuple(sorted(CONTRACT.glob("*.schema.json")))
MANIFEST = CONTRACT / "generated-artifacts.sha256.json"
TARGETS = (
    (CONTRACT / "templates/MessageEnvelope.cs.tpl", ROOT / "ASPSBackend14_J/Common/Generated/Messaging/V1/MessageEnvelope.cs"),
    (CONTRACT / "templates/message_envelope.py.tpl", ROOT / "apps/desktop/win/src/generated/messaging/v1/message_envelope.py"),
    (CONTRACT / "templates/message_envelope.py.tpl", ROOT / "Analyzers/basic-url-analyzer/generated/messaging/v1/message_envelope.py"),
    (CONTRACT / "templates/message-envelope.js.tpl", ROOT / "apps/extension/chrome/generated/messaging/v1/message-envelope.js"),
)

def rendered() -> dict[Path, str]:
    bundle = hashlib.sha256()
    for schema in SCHEMAS:
        bundle.update(schema.name.encode("utf-8"))
        bundle.update(schema.read_bytes())
    digest = bundle.hexdigest()
    outputs = {
        target: template.read_text(encoding="utf-8").replace("__SCHEMA_SHA256__", digest)
        for template, target in TARGETS
    }
    guarded = {
        **{
            str(path.relative_to(ROOT)).replace("\\", "/"):
                hashlib.sha256(content.encode("utf-8")).hexdigest()
            for path, content in outputs.items()
        },
        "ASPSBackend14_J/Common/Generated/Messaging/V1/MessageEnvelopeValidator.cs":
            hashlib.sha256((ROOT / "ASPSBackend14_J/Common/Generated/Messaging/V1/MessageEnvelopeValidator.cs").read_bytes()).hexdigest(),
        "ASPSBackend14_J/Common/Generated/Messaging/V1/MessageEnvelopeFactory.cs":
            hashlib.sha256((ROOT / "ASPSBackend14_J/Common/Generated/Messaging/V1/MessageEnvelopeFactory.cs").read_bytes()).hexdigest(),
    }
    outputs[MANIFEST] = json.dumps(
        {"bundleSha256": digest, "artifacts": guarded},
        indent=2,
        sort_keys=True,
    ) + "\n"
    return outputs

def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    stale = []
    for target, content in rendered().items():
        if args.check:
            if not target.exists() or target.read_text(encoding="utf-8") != content:
                stale.append(target.relative_to(ROOT))
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(content, encoding="utf-8")
    if stale:
        print("Generated messaging bindings are stale:", *stale, sep="\n- ", file=sys.stderr)
        return 1
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
