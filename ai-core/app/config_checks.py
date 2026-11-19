"""Read-only syntax checks for configuration text supplied by the user."""
import json
import xml.etree.ElementTree as ET


def validate_config(text: str, kind: str) -> dict:
    try:
        if kind == "json":
            def reject_constant(value):
                raise ValueError(f"Invalid JSON constant: {value}")
            json.loads(text, parse_constant=reject_constant)
        elif kind == "xml":
            # DayZ configuration files do not require DTDs or entity declarations.
            if "<!DOCTYPE" in text.upper() or "<!ENTITY" in text.upper():
                raise ValueError("DTD and entity declarations are not supported")
            ET.fromstring(text)
        else:
            raise ValueError("Unsupported configuration format")
    except (ValueError, ET.ParseError, RecursionError) as exc:
        return {"valid": False, "error": str(exc)[:500], "scope": "syntax_only"}
    return {"valid": True, "error": None, "scope": "syntax_only"}
