"""
main.py
Monitor de Ponto — BigCard
Polling inteligente: verifica frequentemente só perto dos horários configurados.
"""

import json
import logging
import sys
import time
from datetime import datetime, timedelta
from pathlib import Path

BASE_DIR = Path(__file__).parent
sys.path.insert(0, str(BASE_DIR))

from rhid_client   import RHiDClient
from lunch_tracker import LunchTracker
from state_manager import StateManager


def setup_logging(log_file: str) -> None:
    fmt     = "[%(asctime)s] %(levelname)-8s %(name)s — %(message)s"
    datefmt = "%Y-%m-%d %H:%M:%S"
    handlers = [
        logging.StreamHandler(sys.stdout),
        logging.FileHandler(log_file, encoding="utf-8"),
    ]
    logging.basicConfig(level=logging.INFO, format=fmt, datefmt=datefmt, handlers=handlers)


def load_config() -> dict:
    path = BASE_DIR / "config.json"
    if not path.exists():
        print(f"ERRO: config.json não encontrado em {path}")
        sys.exit(1)
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def is_active_hours(config: dict) -> bool:
    sched = config.get("schedule", {})
    ha    = sched.get("horario_ativo", {})
    try:
        now   = datetime.now().time()
        start = datetime.strptime(ha.get("inicio", "07:00"), "%H:%M").time()
        end   = datetime.strptime(ha.get("fim",    "19:00"), "%H:%M").time()
        return start <= now <= end
    except Exception:
        return True


def get_gatilhos(config: dict) -> list[datetime]:
    """
    Retorna todos os momentos relevantes do dia de hoje:
    para cada evento habilitado, calcula:
      - gatilho_aviso   = horario - antecedencia
      - gatilho_urgente = horario + tolerancia
    Para o almoço usa a janela de saída + duração como referência base.
    """
    sched  = config.get("schedule", {})
    now    = datetime.now()
    hoje   = now.date()
    pontos = []

    def add(horario_str: str, antec: int, toler: int):
        try:
            base = datetime.combine(hoje, datetime.strptime(horario_str, "%H:%M").time())
            pontos.append(base - timedelta(minutes=antec))
            pontos.append(base + timedelta(minutes=toler))
        except Exception:
            pass

    e = sched.get("entrada", {})
    if e.get("enabled"):
        add(e.get("horario", "08:00"),
            int(e.get("antecedencia_minutos", 1)),
            int(e.get("tolerancia_minutos",   3)))

    s = sched.get("saida", {})
    if s.get("enabled"):
        add(s.get("horario", "18:00"),
            int(s.get("antecedencia_minutos", 1)),
            int(s.get("tolerancia_minutos",   3)))

    # Para o almoço, os gatilhos dependem da hora real da 2ª batida
    # Usamos os limites da janela como aproximação para o polling
    a = sched.get("almoco", {})
    if a.get("enabled"):
        janela_fim = a.get("janela_fim", "14:00")
        duracao    = int(a.get("duracao_minutos",      90))
        antec      = int(a.get("antecedencia_minutos",  1))
        toler      = int(a.get("tolerancia_minutos",    3))
        try:
            base = datetime.combine(hoje, datetime.strptime(janela_fim, "%H:%M").time())
            base = base + timedelta(minutes=duracao)
            pontos.append(base - timedelta(minutes=antec))
            pontos.append(base + timedelta(minutes=toler))
        except Exception:
            pass

    return sorted(pontos)


def next_sleep(config: dict) -> int:
    """
    Calcula quantos segundos dormir até o próximo ciclo.
    - Perto de um gatilho (≤ janela_proxima_minutos): polling rápido
    - Longe de qualquer gatilho: polling lento
    """
    mon_cfg  = config.get("monitor", {})
    lento    = int(mon_cfg.get("polling_interval_seconds", 60))
    rapido   = int(mon_cfg.get("polling_proximo_seconds",  30))
    janela   = int(mon_cfg.get("janela_proxima_minutos",    5))

    now      = datetime.now()
    gatilhos = get_gatilhos(config)

    for g in gatilhos:
        diff = abs((g - now).total_seconds())
        if diff <= janela * 60:
            return rapido

    return lento


def run() -> None:
    config  = load_config()
    mon_cfg = config.get("monitor", {})
    setup_logging(str(BASE_DIR / mon_cfg.get("log_file", "ponto_monitor.log")))

    logger = logging.getLogger("ponto_monitor.main")
    nome   = config["person"].get("nome", "?")
    ha     = config.get("schedule", {}).get("horario_ativo", {})

    logger.info("=" * 60)
    logger.info(f"  Monitor de Ponto — {nome}")
    logger.info(f"  Ativo das {ha.get('inicio','07:00')} às {ha.get('fim','19:00')}")
    logger.info("=" * 60)

    state   = StateManager(str(BASE_DIR / mon_cfg.get("state_file", "state.json")))
    rhid    = RHiDClient(config)
    tracker = LunchTracker(config, state)

    cycle = 0
    while True:
        try:
            if not is_active_hours(config):
                logger.debug(f"Fora do horário ativo ({datetime.now().strftime('%H:%M')}) — dormindo 30s")
                time.sleep(30)
                continue

            cycle += 1
            sleep_s = next_sleep(config)
            logger.info(
                f"[Ciclo #{cycle}] {datetime.now().strftime('%Y-%m-%d %H:%M:%S')} "
                f"| próximo em {sleep_s}s"
            )

            records = rhid.get_punch_records_today()
            if records:
                logger.info(f"  {len(records)} registro(s) recebido(s)")
            else:
                logger.info("  Sem registros retornados")

            tracker.process_records(records)

        except KeyboardInterrupt:
            logger.info("Interrompido. Encerrando...")
            break
        except Exception as e:
            logger.error(f"Erro no ciclo #{cycle}: {e}", exc_info=True)

        try:
            time.sleep(sleep_s)
        except KeyboardInterrupt:
            logger.info("Interrompido. Encerrando...")
            break


if __name__ == "__main__":
    run()
