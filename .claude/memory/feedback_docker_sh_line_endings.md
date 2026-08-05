---
name: feedback-docker-sh-line-endings
description: Shell scripts with Windows line endings (CRLF) cause exit code 127 in Alpine Docker containers
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 429ee8b3-85e1-42ff-b1b0-f7d623110a6e
  modified: 2026-08-05T12:24:57.724Z
---

Shell scripts (.sh) created or edited on Windows get CRLF line endings. Alpine Linux Docker containers fail with "not found" (exit code 127) because the `\r` corrupts the shebang line (`#!/bin/sh\r`).

**Why:** Hit this on 2026-08-05 when `docker-entrypoint.sh` in the Angular Admin container kept restarting. The error message is misleading — says "not found" even though the file exists and has `chmod +x`.

**How to apply:**
- When creating `.sh` files for Docker, always convert to LF before committing.
- Add `*.sh text eol=lf` to `.gitattributes` in any repo that uses Docker.
- When debugging Docker exit code 127 on a script that exists, check line endings first.
