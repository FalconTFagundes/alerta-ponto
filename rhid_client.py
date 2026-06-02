"""
rhid_client.py
Consulta batidas de ponto de uma pessoa específica via repp.rhid.com.br
"""

import logging
import requests
from datetime import datetime
from typing import Optional

logger = logging.getLogger("ponto_monitor.rhid")


class RHiDClient:

    def __init__(self, config: dict):
        rhid = config["rhid"]
        self.base_url    = rhid["base_url"].rstrip("/")
        self.repp_url    = rhid.get("repp_url", "https://repp.rhid.com.br").rstrip("/")
        self.username    = rhid["username"]
        self.password    = rhid["password"]
        self.domain      = rhid.get("domain", "")
        self.company_id  = str(rhid.get("company_id", ""))
        self.id_person   = str(config["person"]["id_person"])
        self.token_ttl   = rhid.get("token_refresh_interval_minutes", 50)

        self._token: Optional[str] = None
        self._token_at: Optional[datetime] = None

    # ------------------------------------------------------------------ #
    #  Autenticação                                                         #
    # ------------------------------------------------------------------ #

    def _token_valid(self) -> bool:
        if not self._token or not self._token_at:
            return False
        age = (datetime.now() - self._token_at).total_seconds() / 60
        return age < self.token_ttl

    def _authenticate(self) -> bool:
        url = f"{self.base_url}/login"
        payload = {"email": self.username, "password": self.password, "domain": self.domain}
        try:
            r = requests.post(url, json=payload, timeout=15)
            logger.debug(f"Login {r.status_code}: {r.text[:200]}")
            r.raise_for_status()
            token = r.json().get("accessToken") or r.json().get("token")
            if not token:
                logger.error(f"Token não encontrado na resposta: {r.text[:200]}")
                return False
            self._token = token
            self._token_at = datetime.now()
            logger.info("Autenticação realizada com sucesso")
            return True
        except requests.RequestException as e:
            logger.error(f"Erro no login: {e}")
            return False

    def _ensure_auth(self) -> bool:
        if not self._token_valid():
            return self._authenticate()
        return True

    # ------------------------------------------------------------------ #
    #  Consulta de ponto                                                   #
    # ------------------------------------------------------------------ #

    def get_punch_records_today(self, date: str = None) -> list[dict]:
        if date is None:
            date = datetime.now().strftime("%Y-%m-%d")

        for attempt in range(1, 4):
            if not self._ensure_auth():
                logger.error("Falha na autenticação")
                return []

            url = f"{self.repp_url}/ponto_diario"
            headers = {
                "Authorization": f"Bearer {self._token}",
                "Accept": "application/json, text/plain, */*",
                "X-Cid-Rhid": self.company_id,
                "Origin": "https://www.rhid.com.br",
            }
            params = {"data": date}

            try:
                r = requests.get(url, params=params, headers=headers, timeout=20)
                logger.debug(f"ponto_diario {r.status_code}: {r.text[:300]}")

                if r.status_code == 401:
                    logger.warning(f"401 — renovando token (tentativa {attempt})")
                    self._token = None
                    continue

                r.raise_for_status()
                return self._parse(r.json(), date)

            except requests.RequestException as e:
                logger.warning(f"Erro na requisição (tentativa {attempt}): {e}")

        logger.error("Todas as tentativas falharam")
        return []

    def _parse(self, data: dict, date: str) -> list[dict]:
        if not data or not data.get("status"):
            logger.warning(f"Resposta inválida ou status=false: {str(data)[:200]}")
            return []

        retorno = data.get("retorno", {})

        # Pode vir como lista (múltiplos) ou dict (único)
        items = retorno if isinstance(retorno, list) else [retorno]

        results = []
        for item in items:
            emp_id   = str(item.get("idPerson") or item.get("pis") or "?")
            emp_name = (item.get("nome") or item.get("name") or f"ID {emp_id}").strip()
            afdt     = item.get("afdt", [])

            punches = []
            for b in afdt:
                dt_str = str(b.get("dateTimeStr", ""))
                tipo   = b.get("_typeEntradaSaida", "")

                # Formato dateTimeStr: "202606020755" → YYYYMMDDHHII
                if len(dt_str) >= 12:
                    hora = f"{dt_str[8:10]}:{dt_str[10:12]}"
                else:
                    hora = "00:00"

                punch_type = "ENTRADA" if tipo.upper() in ("E", "ENTRADA", "IN") else \
                             "SAIDA"   if tipo.upper() in ("S", "SAIDA", "OUT") else "UNKNOWN"

                punches.append({"time": hora, "type": punch_type})

            logger.info(f"{emp_name}: {len(punches)} batida(s) — {punches}")
            results.append({
                "employee_id":   emp_id,
                "employee_name": emp_name,
                "date":          date,
                "punches":       punches,
            })

        return results
