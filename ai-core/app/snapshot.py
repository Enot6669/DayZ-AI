"""Read-only status export. No listener, credentials, or remote command execution."""
from datetime import datetime, timezone

import psutil

from .server_monitor import server_processes


def status_snapshot() -> dict:
    memory = psutil.virtual_memory()
    return {
        "schema": "dayz-ai-status-v1",
        "captured_utc": datetime.now(timezone.utc).isoformat(),
        "system": {
            "cpu_percent": psutil.cpu_percent(interval=0.2),
            "memory_total_mb": round(memory.total / 1048576),
            "memory_available_mb": round(memory.available / 1048576),
        },
        "servers": server_processes(),
    }
