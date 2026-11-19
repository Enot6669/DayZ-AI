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
