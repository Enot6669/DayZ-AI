import re

PATTERNS = (
    ("compile_error", re.compile(r"can't compile|unknown type|undefined function", re.I)),
    ("script_error", re.compile(r"script error|null pointer|division by zero", re.I)),
    ("server_error", re.compile(r"\b(error|exception|fatal)\b", re.I)),
)

def analyze_log(text: str) -> list[dict]:
    findings = []
    for line_no, line in enumerate(text.splitlines(), 1):
        for category, pattern in PATTERNS:
            if pattern.search(line):
                findings.append({"line": line_no, "category": category, "message": line[:500]})
                break
    return findings
