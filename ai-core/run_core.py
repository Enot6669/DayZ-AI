"""Entry point for the packaged, local-only AI core."""
import uvicorn
import json
import sys
from app.main import app
from app.snapshot import status_snapshot


if __name__ == "__main__":
    if sys.argv[1:] == ["--snapshot"]:
        print(json.dumps(status_snapshot(), ensure_ascii=True))
    elif not sys.argv[1:]:
        uvicorn.run(app, host="127.0.0.1", port=8765)
    else:
        raise SystemExit("Usage: DayZAI.Core [--snapshot]")
