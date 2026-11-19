# DayZ AI — Developer & Server Doctor

Experimental hybrid DayZ Standalone development and diagnostics platform.

## Stage 1 (prototype)
- WPF Windows desktop application (.NET 10)
- Local FastAPI AI core, bound to 127.0.0.1
- Rule-based DayZ log scanning (RPT, LOG, ADM)
- Optional Ollama generation endpoint
- Python unit/API tests and GitHub Actions Windows build

**Not implemented:** remote agent, server modifications, automatic patches, PBO parser, production installer, model fine-tuning.

## Run AI core (Python 3.12+)
```powershell
cd ai-core
py -m venv .venv
.venv\Scripts\python -m pip install -r requirements.txt
.venv\Scripts\python -m uvicorn app.main:app --host 127.0.0.1 --port 8765
```

## Optional Ollama
Install Ollama separately, pull a coding model such as `qwen2.5-coder:7b`, then POST JSON `{ "prompt": "..." }` to `http://127.0.0.1:8765/ask`.

## Windows desktop
```powershell
dotnet run --project desktop/DayZAI.Desktop/DayZAI.Desktop.csproj
```
Requires Windows and .NET 10 SDK. The GUI currently provides log selection and AI-core health check.

## Tests
```powershell
cd ai-core
.venv\Scripts\python -m pytest -q
```

Security: Local API listens only on loopback. Do not expose this development API publicly. Logs can contain sensitive data; do not share them without review.
