# DayZ AI — Developer & Server Doctor

Experimental hybrid DayZ Standalone development and diagnostics platform.

## Stage 1 (prototype)
- WPF Windows desktop application (.NET 10)
- Local FastAPI AI core, bound to 127.0.0.1
- Rule-based DayZ log scanning (RPT, LOG, ADM)
- Read-only local DayZ server process monitoring via GET /servers (Windows UI: «Найти серверы»)
- System CPU/RAM and process snapshots (`GET /status`). Export a snapshot on a server computer using `DayZAI.Core.exe --snapshot` and import its JSON in the desktop app. This is a manual offline exchange, not an authenticated live remote agent.
- Confirmed local launch using the selected profile, default `serverDZ.cfg`, game port and semicolon-separated installed `@mod` folders. No shell commands or automatic AI actions. Only one DayZ process is supported at a time; stop/restart must still be done using your existing server administration tools. A created process does not prove game server readiness.
- Launch audit records in `%LOCALAPPDATA%/DayZAI/operations.jsonl`.
- Steam Manager for installing/updating the stable DayZ dedicated server (223350), downloading Workshop items (221100) by numeric item ID, and installing a new downloaded mod as `@Workshop_ID` with non-conflicting `.bikey` files. No existing mod is replaced. Operations require user confirmation and a stopped local server.
- JSON/XML syntax validation without editing files (not a DayZ schema check)
- Local server profiles stored in `%LOCALAPPDATA%/DayZAI/profiles.json`, with read-only checks for the server executable, default config, installed `@` mods, PBOs and keys. Does not resolve dependency graphs or verify signatures.
- Single configuration file backup (JSON/XML/CFG/INI/TXT, up to 10 MiB) in a ZIP with SHA-256 and metadata. Restore verifies the checksum and requires confirmation before creating a new file; existing files are never overwritten. These archives may contain passwords; store them securely. Not a live-world/server snapshot.
- Optional Ollama generation endpoint and desktop prompt interface; generated code is never executed automatically
- Bundled offline reference index (14 curated DayZ topics with Bohemia documentation links). Search in the UI without Ollama, or supply relevant excerpts to Ollama for every question. This is retrieval-augmented prompting, not model training or runtime verification. Separate patch addons are the required repair strategy; original PBOs are never edited by the assistant.
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
For a packaged preview, extract the entire Windows ZIP, run `DayZAI.Core.exe` first, then `DayZAI.Desktop.exe`. Stop the core by closing its console when finished. Ollama is optional and separately installed. Preview releases are published after successful CI tests and the packaged core health check.

To collect an offline status report from an authorized remote Windows machine, run `cmd /c "DayZAI.Core.exe --snapshot > status.json"` there and transfer the JSON via your existing secure administration channel. Do not expose the core API to the network. Imported reports are not authenticated or live.

## Steam Manager
1. Add a local profile (an existing empty folder is fine for a new installation).
2. Open Steam / Workshop and select the official SteamCMD executable. The Valve ZIP download button opens the official location; extract it in its own directory.
3. Enter only your Steam account name. SteamCMD handles login, passwords, Steam Guard and Steam entitlement checks in its own console. DayZ AI does not collect passwords, API keys or Guard codes. SteamCMD may cache its own session: protect its folder.
4. Choose install/update server, or download a Workshop item by ID. Confirm the operation after stopping all DayZ processes and backing up configurations.
5. After a successful Workshop download, choose install downloaded mod, then explicitly add `@Workshop_ID` to the launch list. No automatic dependency resolution, compatibility verification or replacement of existing mods is performed.

Steam Guard authenticates the Steam login, not arbitrary DayZ AI administrative operations. Local operation confirmations are separate; the app does not claim a Steam Web API ownership verification without publisher credentials. SteamCMD exit codes and existing files alone are not proof of a fresh successful download. If installation fails, check SteamCMD output and any `.dayzai-stage-*` folder created during staging. Do not update a running server.

For development:
```powershell
dotnet run --project desktop/DayZAI.Desktop/DayZAI.Desktop.csproj
```
Requires Windows and .NET 10 SDK. The GUI currently provides log selection, local server process listing and AI-core health check.

## Tests
```powershell
cd ai-core
.venv\Scripts\python -m pytest -q
```

On Windows: `dotnet run --project desktop/DayZAI.Workspace.Tests/DayZAI.Workspace.Tests.csproj --configuration Release` from the repository root. Tests use temporary folders, not a live server.

Security: Local API listens only on loopback. Do not expose this development API publicly. Logs can contain sensitive data; do not share them without review.
