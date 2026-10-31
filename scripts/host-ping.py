#!/usr/bin/env python3
"""Sends a ping to the browser helper the way the browser does and prints the answer (used by CI)."""
import json, struct, subprocess, sys

host = sys.argv[1]
body = json.dumps({"type": "ping"}).encode()
out = subprocess.run([host], input=struct.pack("<I", len(body)) + body, capture_output=True, timeout=10).stdout
n = struct.unpack("<I", out[:4])[0]
reply = json.loads(out[4 : 4 + n])
print(reply)
sys.exit(0 if reply.get("ok") else 1)
