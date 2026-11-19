from fastapi.testclient import TestClient
from app.main import app
from app.diagnostics import analyze_log

client = TestClient(app)

def test_health():
    assert client.get('/health').json()['status'] == 'ok'

def test_log_detection():
    result = analyze_log('hello\nUnknown type VVI_Manager\nnormal')
    assert result == [{'line': 2, 'category': 'compile_error', 'message': 'Unknown type VVI_Manager'}]

def test_analyze_endpoint():
    assert len(client.post('/analyze', json={'text': 'Script error: null pointer'}).json()['findings']) == 1

def test_empty_log():
    assert analyze_log('Server started successfully') == []
