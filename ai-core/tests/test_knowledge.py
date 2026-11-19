import io
import json

from fastapi.testclient import TestClient

from app.main import app
from app.knowledge import ENTRIES, search


client = TestClient(app)


def test_knowledge_index():
    assert len(ENTRIES) >= 14
    assert len({entry.id for entry in ENTRIES}) == len(ENTRIES)
    assert all(entry.source.startswith('https://community.bistudio.com/wiki/') for entry in ENTRIES)


def test_search_exact_and_russian():
    assert search('modded class override super')[0]['id'] == 'modded-class'
    assert search('requiredAddons CfgPatches')[0]['id'] == 'addon-dependencies'
    assert search('ошибка компиляции unknown type')[0]['id'] == 'compile-diagnostics'
    assert search('подписи ключи bikey')[0]['id'] == 'signatures'
    assert search('types.xml экономика')[0]['id'] == 'economy'
    assert search('zebraquasarunrelated') == []


def test_api_returns_references_without_ollama():
    response = client.get('/knowledge', params={'q': 'отдельный патч оригинальный pbo', 'limit': 2})
    assert response.status_code == 200
    assert len(response.json()['results']) == 2
    assert any(item['id'] in ('patch-workflow', 'modded-class') for item in response.json()['results'])
    assert client.get('/knowledge', params={'q': 'test', 'limit': 9}).status_code == 422


def test_ask_injects_context_and_keeps_user_prompt_separate(monkeypatch):
    from app import main
    captured = {}

    def fake_open(request, timeout):
        captured.update(json.loads(request.data))
        return io.BytesIO(b'{"response":"example answer"}')

    monkeypatch.setattr(main.urllib.request, 'urlopen', fake_open)
    question = 'How to use modded class and override super?'
    result = client.post('/ask', json={'prompt': question}).json()
    assert captured['prompt'] == question
    assert '[modded-class]' in captured['system']
    assert 'never modify the original PBO' in captured['system']
    assert result['answer'] == 'example answer'
    assert result['references'][0]['id'] == 'modded-class'


def test_ask_without_matching_context(monkeypatch):
    from app import main
    captured = {}

    def fake_open(request, timeout):
        captured.update(json.loads(request.data))
        return io.BytesIO(b'{"response":"unknown"}')

    monkeypatch.setattr(main.urllib.request, 'urlopen', fake_open)
    result = client.post('/ask', json={'prompt': 'zebraquasarunrelated'}).json()
    assert result['references'] == []
    assert 'No matching reference' in captured['system']
