"""Safe server operation proposals; this module never executes commands."""
from pydantic import BaseModel, Field
from typing import Literal

Operation = Literal["start", "stop", "restart", "backup"]

class ActionRequest(BaseModel):
    operation: Operation
    pid: int = Field(gt=0)

def propose_action(request: ActionRequest) -> dict:
    descriptions = {
        "start": "Запуск требует настроенного профиля сервера и отдельного агента.",
        "stop": "Остановка может отключить игроков и привести к потере несохранённого прогресса.",
        "restart": "Перезапуск прервёт текущие игровые сессии.",
        "backup": "Резервная копия требует настроенного каталога и проверки свободного места.",
    }
    return {
        "operation": request.operation,
        "pid": request.pid,
        "description": descriptions[request.operation],
        "requires_confirmation": True,
        "executed": False,
        "status": "proposal_only",
    }
