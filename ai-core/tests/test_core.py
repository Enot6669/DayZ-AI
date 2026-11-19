from fastapi.testclient import TestClient
from app.main import app
from app.diagnostics import analyze_log

client = TestClient(app)

def test_health():
    assert client.get('/health').json()['status'] == 'ok'

def test_log_detection():
    result = analyze_log('hello\nUnknown type VVI_Manager\nnormal')
    assert len(result) == 1
    assert result[0]['line'] == 2
    assert result[0]['category'] == 'compile_error'
    assert result[0]['severity'] == 'high'
    assert 'зависимостей' in result[0]['advice']

def test_analyze_endpoint():
    result = client.post('/analyze', json={'text': 'Script error: null pointer'})
    assert result.status_code == 200
    assert result.json()['findings'][0]['category'] == 'script_error'

def test_empty_log():
    assert analyze_log('Server started successfully') == []

def test_missing_mod():
    assert analyze_log('Missing addon DZ_Data')[0]['category'] == 'missing_mod'

def test_config_error():
    assert analyze_log('XML parser error')[0]['category'] == 'config_error'

def test_precedence():
    assert analyze_log("Can't compile script: fatal error")[0]['category'] == 'compile_error'

def test_line_truncation():
    assert len(analyze_log('ERROR ' + 'x' * 900)[0]['message']) == 500

def test_reject_oversize():
    response = client.post('/analyze', json={'text': 'a' * 2_000_001})
    assert response.status_code == 422

def test_generic_error():
    assert analyze_log('Server ERROR while starting')[0]['category'] == 'server_error'

def test_missing_pbo():
    assert analyze_log("Cannot open file addons\\\\example.pbo")[0]['category'] == 'missing_mod'

def test_no_false_positive_for_error_word():
    assert analyze_log('errorless run completed') == []


def test_servers_endpoint(monkeypatch):
    from app import main
    monkeypatch.setattr(main, "server_processes", lambda: [{"pid": 123, "name": "DayZServer_x64.exe"}])
    result = client.get('/servers')
    assert result.status_code == 200
    assert result.json()['mode'] == 'read_only'
    assert result.json()['servers'][0]['pid'] == 123

def test_process_filter(monkeypatch):
    from app import server_monitor
    class FakeProcess:
        info = {"pid": 42, "name": "DayZServer_x64.exe", "status": "running", "memory_info": None, "cpu_percent": 0.0}
    class OtherProcess:
        info = {"pid": 43, "name": "notepad.exe"}
    monkeypatch.setattr(server_monitor.psutil, "process_iter", lambda attrs: [OtherProcess(), FakeProcess()])
    assert [p["pid"] for p in server_monitor.server_processes()] == [42]


def test_preview_never_executes():
    for operation in ('start', 'stop', 'restart', 'backup'):
        response = client.post('/servers/action-preview', json={'operation': operation, 'pid': 42})
        assert response.status_code == 200
        assert response.json()['executed'] is False
        assert response.json()['requires_confirmation'] is True


def test_invalid_action():
    assert client.post('/servers/action-preview', json={'operation': 'shell', 'pid': 42}).status_code == 422


def test_config_validation():
    for kind, text, valid in [
        ('json', '{"enabled":true}', True), ('json', '{bad}', False),
        ('json', 'NaN', False), ('xml', '<types/>', True),
        ('xml', '<types>', False), ('xml', '<!DOCTYPE types><types/>', False),
    ]:
        result = client.post('/config/validate', json={'kind': kind, 'text': text})
        assert result.status_code == 200
        assert result.json()['valid'] is valid
        assert result.json()['scope'] == 'syntax_only'


def test_status_snapshot(monkeypatch):
    from app import snapshot
    from types import SimpleNamespace
    monkeypatch.setattr(snapshot.psutil, 'virtual_memory', lambda: SimpleNamespace(total=8 * 1048576, available=3 * 1048576))
    monkeypatch.setattr(snapshot.psutil, 'cpu_percent', lambda interval: 12.5)
    monkeypatch.setattr(snapshot, 'server_processes', lambda: [{'pid': 42, 'name': 'DayZServer_x64.exe'}])
    data = client.get('/status').json()
    assert data['schema'] == 'dayz-ai-status-v1'
    assert data['system'] == {'cpu_percent': 12.5, 'memory_total_mb': 8, 'memory_available_mb': 3}
    assert data['servers'][0]['pid'] == 42
