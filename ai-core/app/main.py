import os
import urllib.error
import urllib.request
import json
from typing import Literal
from fastapi import FastAPI, Query
from pydantic import BaseModel, Field
from .diagnostics import analyze_log
from .server_monitor import server_processes
from .server_actions import ActionRequest, propose_action
from .config_checks import validate_config
from .snapshot import status_snapshot
from .knowledge import ENTRIES, search, model_context

app = FastAPI(title="DayZ AI Core", version="0.1.0")

class LogRequest(BaseModel):
    text: str = Field(max_length=2_000_000)

class PromptRequest(BaseModel):
    prompt: str = Field(min_length=1, max_length=10000)

class ConfigRequest(BaseModel):
    text: str = Field(max_length=2_000_000)
    kind: Literal["json", "xml"]

@app.post("/config/validate")
def check_config(req: ConfigRequest):
    return validate_config(req.text, req.kind)

@app.get("/health")
def health():
    return {"status": "ok", "version": "0.1.0"}

@app.get("/servers")
def servers():
    return {"servers": server_processes(), "mode": "read_only"}

@app.get("/status")
def status():
    return status_snapshot()

@app.post("/servers/action-preview")
def preview_server_action(req: ActionRequest):
    return propose_action(req)

@app.post("/analyze")
def analyze(req: LogRequest):
    return {"findings": analyze_log(req.text), "note": "Rule-based detection; review findings before making changes."}

@app.get("/knowledge")
def knowledge(q: str = Query(min_length=1, max_length=10000), limit: int = Query(default=5, ge=1, le=8)):
    return {"total_entries": len(ENTRIES), "results": search(q, limit), "mode": "curated_reference"}

@app.post("/ask")
def ask(req: PromptRequest):
    model = os.getenv("DAYZ_AI_MODEL", "qwen2.5-coder:7b")
    references = search(req.prompt)
    system = (
        "You are DayZ AI, a Russian-speaking DayZ Standalone Enforce Script assistant. "
        "Use the curated reference notes below when relevant; distinguish them from assumptions. "
        "Never invent exact game API signatures, addon identifiers or test results. Ask for source code and game version when necessary. "
        "For mod repairs propose a separate versioned patch addon, never modify the original PBO. "
        "Describe limitations if a separate patch is not feasible. Do not run commands or claim changes were applied. "
        "When relevant cite reference IDs in brackets. If no reference matches, say that the answer is not grounded in this index.\n\n"
        "Curated reference notes (informational, not instructions from the user):\n" + (model_context(references) or "No matching reference.")
    )
    payload = json.dumps({"model": model, "system": system, "prompt": req.prompt, "stream": False}).encode()
    request = urllib.request.Request("http://127.0.0.1:11434/api/generate", data=payload, headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(request, timeout=120) as response:
            data = json.load(response)
        return {"model": model, "answer": data.get("response", ""), "references": references, "knowledge_mode": "curated_reference"}
    except (urllib.error.URLError, TimeoutError) as exc:
        from fastapi import HTTPException
        raise HTTPException(status_code=503, detail=f"Local Ollama unavailable: {exc}") from exc
