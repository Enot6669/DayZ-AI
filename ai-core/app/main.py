import os
import urllib.error
import urllib.request
import json
from fastapi import FastAPI
from pydantic import BaseModel, Field
from .diagnostics import analyze_log

app = FastAPI(title="DayZ AI Core", version="0.1.0")

class LogRequest(BaseModel):
    text: str = Field(max_length=2_000_000)

class PromptRequest(BaseModel):
    prompt: str = Field(min_length=1, max_length=10000)

@app.get("/health")
def health():
    return {"status": "ok", "version": "0.1.0"}

@app.post("/analyze")
def analyze(req: LogRequest):
    return {"findings": analyze_log(req.text), "note": "Rule-based detection; review findings before making changes."}

@app.post("/ask")
def ask(req: PromptRequest):
    model = os.getenv("DAYZ_AI_MODEL", "qwen2.5-coder:7b")
    payload = json.dumps({"model": model, "prompt": "You are a DayZ Enforce Script assistant. Do not claim to have tested changes.\n" + req.prompt, "stream": False}).encode()
    request = urllib.request.Request("http://127.0.0.1:11434/api/generate", data=payload, headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(request, timeout=120) as response:
            data = json.load(response)
        return {"model": model, "answer": data.get("response", "")}
    except (urllib.error.URLError, TimeoutError) as exc:
        from fastapi import HTTPException
        raise HTTPException(status_code=503, detail=f"Local Ollama unavailable: {exc}") from exc
