"""Read-only DayZ server process monitoring.

Never starts, stops, or modifies processes. Local host only.
"""
import psutil

SERVER_EXECUTABLES = {"dayzserver_x64.exe", "dayzserver.exe"}

def server_processes() -> list[dict]:
    found = []
    for proc in psutil.process_iter(["pid", "name", "status", "memory_info", "cpu_percent"]):
        try:
            info = proc.info
            if (info.get("name") or "").lower() not in SERVER_EXECUTABLES:
                continue
            memory = info.get("memory_info")
            found.append({
                "pid": info["pid"],
                "name": info["name"],
                "status": info.get("status"),
                "memory_mb": round(memory.rss / 1048576, 1) if memory else None,
                "cpu_percent": info.get("cpu_percent"),
            })
        except (psutil.NoSuchProcess, psutil.AccessDenied, psutil.ZombieProcess):
            continue
    return sorted(found, key=lambda item: item["pid"])
