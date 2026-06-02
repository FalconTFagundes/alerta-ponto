"""
state_manager.py
Responsável pela leitura e gravação do estado no arquivo state.json.
Nenhum banco de dados — tudo em arquivo local.
"""

import json
import os
import logging
from datetime import datetime
from typing import Any

logger = logging.getLogger("ponto_monitor.state")


class StateManager:
    def __init__(self, state_file: str = "state.json"):
        self.state_file = state_file
        self._ensure_file_exists()

    # ------------------------------------------------------------------ #
    #  Persistência                                                         #
    # ------------------------------------------------------------------ #

    def _ensure_file_exists(self) -> None:
        if not os.path.exists(self.state_file):
            self._write({})
            logger.info(f"Arquivo de estado criado: {self.state_file}")

    def _read(self) -> dict:
        try:
            with open(self.state_file, "r", encoding="utf-8") as f:
                return json.load(f)
        except (json.JSONDecodeError, OSError) as e:
            logger.error(f"Erro ao ler state.json: {e} — retornando estado vazio")
            return {}

    def _write(self, data: dict) -> None:
        try:
            with open(self.state_file, "w", encoding="utf-8") as f:
                json.dump(data, f, indent=2, ensure_ascii=False, default=str)
        except OSError as e:
            logger.error(f"Erro ao gravar state.json: {e}")

    # ------------------------------------------------------------------ #
    #  API pública                                                          #
    # ------------------------------------------------------------------ #

    def get_all(self) -> dict:
        """Retorna todo o estado atual."""
        return self._read()

    def get_employee(self, employee_id: str) -> dict | None:
        """Retorna o registro de um funcionário ou None se não existir."""
        return self._read().get(str(employee_id))

    def upsert_employee(self, employee_id: str, data: dict) -> None:
        """Cria ou atualiza o registro de um funcionário."""
        state = self._read()
        state[str(employee_id)] = data
        self._write(state)
        logger.debug(f"Estado atualizado para funcionário {employee_id}: {data}")

    def remove_employee(self, employee_id: str) -> None:
        """Remove o registro de um funcionário do estado."""
        state = self._read()
        if str(employee_id) in state:
            del state[str(employee_id)]
            self._write(state)
            logger.debug(f"Registro removido do estado: funcionário {employee_id}")

    def mark_notified(self, employee_id: str) -> None:
        """Marca o funcionário como já notificado para evitar alertas duplicados."""
        state = self._read()
        emp_key = str(employee_id)
        if emp_key in state:
            state[emp_key]["notified"] = True
            self._write(state)
            logger.debug(f"Funcionário {employee_id} marcado como notificado")

    def is_notified(self, employee_id: str) -> bool:
        record = self.get_employee(str(employee_id))
        return bool(record.get("notified", False)) if record else False

    def snapshot(self) -> str:
        """Retorna uma representação legível do estado atual (para logs)."""
        state = self._read()
        if not state:
            return "(estado vazio)"
        lines = []
        for emp_id, rec in state.items():
            lines.append(
                f"  [{emp_id}] almoço={rec.get('lunch_start', '?')} "
                f"retorno_esperado={rec.get('expected_return', '?')} "
                f"notificado={rec.get('notified', False)}"
            )
        return "\n".join(lines)
