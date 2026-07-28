"""Backend-side CLI compatibility client for the isolated Analyzer worker."""

import argparse
import http.client
import json
import os
import socket
import sys


class UnixHTTPConnection(http.client.HTTPConnection):
    def __init__(self, socket_path, timeout=30):
        super().__init__("localhost", timeout=timeout)
        self.socket_path = socket_path

    def connect(self):
        self.sock = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
        self.sock.settimeout(self.timeout)
        self.sock.connect(self.socket_path)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("url")
    parser.add_argument("--json", action="store_true")
    args, _ = parser.parse_known_args()
    socket_path = os.environ.get(
        "ASPS_ANALYZER_SOCKET",
        "/run/asps-analyzer/analyzer.sock",
    )
    connection = UnixHTTPConnection(socket_path)
    body = json.dumps({"url": args.url}).encode("utf-8")
    connection.request(
        "POST",
        "/analyze/raw",
        body=body,
        headers={"Content-Type": "application/json"},
    )
    response = connection.getresponse()
    payload = response.read().decode("utf-8")
    if response.status != 200:
        print(payload, file=sys.stderr)
        return 1
    print(payload)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
