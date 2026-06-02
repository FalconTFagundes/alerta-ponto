"""
lunch_tracker.py
Dois níveis de alerta por evento:
  - 1º alerta (AVISO):   antecedencia_minutos antes do prazo
  - 2º alerta (URGENTE): tolerancia_minutos após o prazo, se ainda não bateu
"""

import logging
from datetime import datetime, timedelta
from typing import Optional

from state_manager import StateManager
from alerter import show_alert

logger = logging.getLogger("ponto_monitor.tracker")


class LunchTracker:

    def __init__(self, config: dict, state_manager: StateManager):
        self.state     = state_manager
        self.id_person = str(config["person"]["id_person"])
        self.nome      = config["person"].get("nome", f"ID {self.id_person}")

        sched = config.get("schedule", {})

        e = sched.get("entrada", {})
        self.entrada_enabled      = e.get("enabled", False)
        self.entrada_horario      = self._pt(e.get("horario", "08:00"))
        self.entrada_antecedencia = timedelta(minutes=int(e.get("antecedencia_minutos", 1)))
        self.entrada_tolerancia   = timedelta(minutes=int(e.get("tolerancia_minutos", 3)))

        a = sched.get("almoco", {})
        self.almoco_enabled      = a.get("enabled", True)
        self.almoco_janela_ini   = self._pt(a.get("janela_inicio", "11:00"))
        self.almoco_janela_fim   = self._pt(a.get("janela_fim", "14:00"))
        self.almoco_duracao      = timedelta(minutes=int(a.get("duracao_minutos", 90)))
        self.almoco_antecedencia = timedelta(minutes=int(a.get("antecedencia_minutos", 1)))
        self.almoco_tolerancia   = timedelta(minutes=int(a.get("tolerancia_minutos", 3)))

        s = sched.get("saida", {})
        self.saida_enabled      = s.get("enabled", False)
        self.saida_horario      = self._pt(s.get("horario", "18:00"))
        self.saida_antecedencia = timedelta(minutes=int(s.get("antecedencia_minutos", 1)))
        self.saida_tolerancia   = timedelta(minutes=int(s.get("tolerancia_minutos", 3)))

    # ------------------------------------------------------------------ #
    #  Entrada principal                                                    #
    # ------------------------------------------------------------------ #

    def process_records(self, records: list[dict]) -> None:
        record = next((r for r in records if str(r["employee_id"]) == self.id_person), None)
        today  = datetime.now().strftime("%Y-%m-%d")
        punches = []

        if record:
            date = record.get("date", today)
            for p in record.get("punches", []):
                ts = self._bdt(date, p["time"])
                if ts:
                    punches.append({"ts": ts, "type": p["type"]})
            punches.sort(key=lambda x: x["ts"])
            logger.debug(f"Batidas {self.nome}: {[(p['ts'].strftime('%H:%M'), p['type']) for p in punches]}")

        self._check_entrada(punches)
        self._check_almoco(punches, today)
        self._check_saida(punches)

    # ------------------------------------------------------------------ #
    #  ENTRADA                                                              #
    # ------------------------------------------------------------------ #

    def _check_entrada(self, punches: list) -> None:
        if not self.entrada_enabled:
            return

        now       = datetime.now()
        hoje      = now.strftime("%Y%m%d")
        key_aviso = f"{self.id_person}_entrada_aviso_{hoje}"
        key_urg   = f"{self.id_person}_entrada_urgente_{hoje}"

        tem_entrada = len(punches) >= 1
        if tem_entrada:
            return  # Já bateu — nada a fazer

        horario_dt   = datetime.combine(now.date(), self.entrada_horario)
        gatilho_aviso = horario_dt - self.entrada_antecedencia   # ex: 07:59
        gatilho_urg   = horario_dt + self.entrada_tolerancia     # ex: 08:03

        horario_fmt = self.entrada_horario.strftime("%H:%M")

        # 2º alerta — URGENTE
        if now >= gatilho_urg and not self.state.get_employee(key_urg):
            logger.warning(f"[{self._now()}] URGENTE ENTRADA: {self.nome}")
            self.state.upsert_employee(key_urg, {"ts": now.isoformat()})
            show_alert(self.nome, tipo="ENTRADA", urgente=True, horario=horario_fmt)
            return

        # 1º alerta — AVISO
        if now >= gatilho_aviso and not self.state.get_employee(key_aviso):
            logger.warning(f"[{self._now()}] AVISO ENTRADA: {self.nome}")
            self.state.upsert_employee(key_aviso, {"ts": now.isoformat()})
            show_alert(self.nome, tipo="ENTRADA", urgente=False, horario=horario_fmt)

    # ------------------------------------------------------------------ #
    #  ALMOÇO                                                               #
    # ------------------------------------------------------------------ #

    def _check_almoco(self, punches: list, date: str) -> None:
        if not self.almoco_enabled:
            return

        key_estado = f"{self.id_person}_almoco"

        # 3ª batida = retorno confirmado
        if len(punches) >= 3:
            if self.state.get_employee(key_estado):
                logger.info(f"[{self._now()}] Retorno almoço — {self.nome} às {punches[2]['ts'].strftime('%H:%M')}")
                self.state.remove_employee(key_estado)
            # Limpa também chaves de aviso/urgente do almoço de hoje
            hoje = datetime.now().strftime("%Y%m%d")
            self.state.remove_employee(f"{self.id_person}_almoco_aviso_{hoje}")
            self.state.remove_employee(f"{self.id_person}_almoco_urgente_{hoje}")
            return

        # 2ª batida = saída almoço
        if len(punches) >= 2:
            segunda = punches[1]
            if self._in_lunch_window(segunda["ts"]):
                if self.state.get_employee(key_estado) is None:
                    esperado  = segunda["ts"] + self.almoco_duracao
                    gatilho_a = esperado - self.almoco_antecedencia
                    gatilho_u = esperado + self.almoco_tolerancia
                    self.state.upsert_employee(key_estado, {
                        "lunch_start":     segunda["ts"].isoformat(),
                        "expected_return": esperado.isoformat(),
                        "gatilho_aviso":   gatilho_a.isoformat(),
                        "gatilho_urgente": gatilho_u.isoformat(),
                        "nome":            self.nome,
                    })
                    logger.info(
                        f"[{self._now()}] Saída almoço — {self.nome} "
                        f"às {segunda['ts'].strftime('%H:%M')} | "
                        f"retorno {esperado.strftime('%H:%M')} | "
                        f"aviso {gatilho_a.strftime('%H:%M')} | "
                        f"urgente {gatilho_u.strftime('%H:%M')}"
                    )

        # Verifica alertas
        rec = self.state.get_employee(key_estado)
        if not rec:
            return

        now   = datetime.now()
        hoje  = now.strftime("%Y%m%d")
        key_a = f"{self.id_person}_almoco_aviso_{hoje}"
        key_u = f"{self.id_person}_almoco_urgente_{hoje}"

        saida_fmt    = datetime.fromisoformat(rec["lunch_start"]).strftime("%H:%M")
        esperado_fmt = datetime.fromisoformat(rec["expected_return"]).strftime("%H:%M")
        gatilho_u    = self._pi(rec.get("gatilho_urgente"))
        gatilho_a    = self._pi(rec.get("gatilho_aviso"))

        # 2º alerta — URGENTE
        if gatilho_u and now >= gatilho_u and not self.state.get_employee(key_u):
            logger.warning(f"[{self._now()}] URGENTE ALMOÇO: {self.nome}")
            self.state.upsert_employee(key_u, {"ts": now.isoformat()})
            show_alert(self.nome, tipo="ALMOCO", urgente=True, saida=saida_fmt, esperado=esperado_fmt)
            return

        # 1º alerta — AVISO
        if gatilho_a and now >= gatilho_a and not self.state.get_employee(key_a):
            logger.warning(f"[{self._now()}] AVISO ALMOÇO: {self.nome}")
            self.state.upsert_employee(key_a, {"ts": now.isoformat()})
            show_alert(self.nome, tipo="ALMOCO", urgente=False, saida=saida_fmt, esperado=esperado_fmt)

    # ------------------------------------------------------------------ #
    #  SAÍDA                                                                #
    # ------------------------------------------------------------------ #

    def _check_saida(self, punches: list) -> None:
        if not self.saida_enabled:
            return

        now       = datetime.now()
        hoje      = now.strftime("%Y%m%d")
        key_aviso = f"{self.id_person}_saida_aviso_{hoje}"
        key_urg   = f"{self.id_person}_saida_urgente_{hoje}"

        # Número par de batidas > 0 = última foi saída = já bateu saída final
        tem_saida_final = len(punches) > 0 and len(punches) % 2 == 0
        if tem_saida_final:
            return

        horario_dt    = datetime.combine(now.date(), self.saida_horario)
        gatilho_aviso = horario_dt - self.saida_antecedencia
        gatilho_urg   = horario_dt + self.saida_tolerancia
        horario_fmt   = self.saida_horario.strftime("%H:%M")

        # 2º alerta — URGENTE
        if now >= gatilho_urg and not self.state.get_employee(key_urg):
            logger.warning(f"[{self._now()}] URGENTE SAÍDA: {self.nome}")
            self.state.upsert_employee(key_urg, {"ts": now.isoformat()})
            show_alert(self.nome, tipo="SAIDA", urgente=True, horario=horario_fmt)
            return

        # 1º alerta — AVISO
        if now >= gatilho_aviso and not self.state.get_employee(key_aviso):
            logger.warning(f"[{self._now()}] AVISO SAÍDA: {self.nome}")
            self.state.upsert_employee(key_aviso, {"ts": now.isoformat()})
            show_alert(self.nome, tipo="SAIDA", urgente=False, horario=horario_fmt)

    # ------------------------------------------------------------------ #
    #  Helpers                                                              #
    # ------------------------------------------------------------------ #

    def _in_lunch_window(self, ts: datetime) -> bool:
        return self.almoco_janela_ini <= ts.time() <= self.almoco_janela_fim

    @staticmethod
    def _pt(s: str):
        try:
            return datetime.strptime(s, "%H:%M").time()
        except Exception:
            return datetime.strptime("00:00", "%H:%M").time()

    @staticmethod
    def _bdt(date_str: str, time_str: str) -> Optional[datetime]:
        try:
            return datetime.strptime(f"{date_str} {time_str[:5]}", "%Y-%m-%d %H:%M")
        except Exception:
            return None

    @staticmethod
    def _pi(s: str) -> Optional[datetime]:
        try:
            return datetime.fromisoformat(s)
        except Exception:
            return None

    @staticmethod
    def _now() -> str:
        return datetime.now().strftime("%Y-%m-%d %H:%M:%S")
