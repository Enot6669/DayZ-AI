"""Conservative, explainable DayZ server log diagnostics.

Detection is heuristic: findings are leads, not verified root causes.
"""
import re

RULES = (
    ("compile_error", "high", r"can't compile|unknown type|undefined function|syntax error",
     "Проверьте файл скрипта, порядок загрузки модов и наличие зависимостей."),
    ("missing_mod", "high", r"missing addon|cannot open file.*\\.pbo|can't find.*\\.pbo|dependency.*not found",
     "Проверьте наличие модов, ключей и порядок запуска сервера."),
    ("config_error", "medium", r"xml parser|xml parse|json parse|invalid json|invalid xml",
     "Проверьте синтаксис и структуру конфигурации; перед изменениями сделайте копию."),
    ("script_error", "medium", r"script error|null pointer|division by zero",
     "Проверьте стек вызовов и связанные моды; не исправляйте без воспроизведения."),
    ("server_error", "medium", r"\\b(error|exception|fatal)\\b",
     "Изучите соседние строки лога и определите источник сообщения."),
)
COMPILED = [(category, severity, re.compile(pattern, re.I), advice)
            for category, severity, pattern, advice in RULES]

def analyze_log(text: str) -> list[dict]:
    findings = []
    for line_no, line in enumerate(text.splitlines(), 1):
        for category, severity, pattern, advice in COMPILED:
            if pattern.search(line):
                findings.append({
                    "line": line_no,
                    "category": category,
                    "severity": severity,
                    "message": line[:500],
                    "advice": advice,
                })
                break
    return findings
