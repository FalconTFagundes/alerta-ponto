# 🕐 Monitor de Ponto — BigCard / RHiD

Sistema local em Python que monitora batidas de ponto via API do RHiD (Control iD)
e alerta quando um funcionário esquece de retornar do almoço.

**Sem banco de dados.** Todo o estado é salvo em `state.json`.

---

## 📁 Estrutura do Projeto

```
ponto_monitor/
├── main.py            ← Loop principal / entry point
├── rhid_client.py     ← Integração com a API REST do RHiD
├── lunch_tracker.py   ← Lógica de negócio (detectar almoço / alertas)
├── notifier.py        ← Envio via Telegram + console
├── state_manager.py   ← Leitura/gravação do state.json
├── config.json        ← Credenciais e parâmetros (editar antes de usar)
├── requirements.txt   ← Dependências Python
├── state.json         ← Gerado automaticamente em runtime
└── ponto_monitor.log  ← Gerado automaticamente em runtime
```

---

## ⚙️ Configuração

Edite o arquivo `config.json` antes de rodar:

```json
{
  "rhid": {
    "base_url": "https://www.rhid.com.br/v2",
    "username": "seu_usuario",
    "password": "sua_senha",
    "company_id": "id_da_empresa",
    "token_refresh_interval_minutes": 50
  },
  "telegram": {
    "enabled": true,
    "bot_token": "123456789:ABCdef...",
    "chat_id": "-100123456789"
  },
  "monitor": {
    "polling_interval_seconds": 60,
    "lunch_window_start": "11:00",
    "lunch_window_end": "14:00",
    "lunch_duration_minutes": 90,
    "alert_grace_period_minutes": 5
  }
}
```

### Como obter o Bot Token e Chat ID do Telegram

1. Abra o Telegram e procure por **@BotFather**
2. Envie `/newbot` e siga as instruções → você receberá o `bot_token`
3. Para obter o `chat_id`:
   - Adicione o bot ao grupo ou canal desejado
   - Acesse `https://api.telegram.org/bot<SEU_TOKEN>/getUpdates` no navegador
   - O `chat_id` aparecerá no JSON retornado (valores negativos = grupos)

---

## 🚀 Instalação e Execução

### Pré-requisitos

- Python 3.10 ou superior
- Windows 10/11 (também funciona em Linux/macOS)

### Passo a passo

```bash
# 1. Entre na pasta do projeto
cd ponto_monitor

# 2. (Recomendado) Crie um ambiente virtual
python -m venv venv
venv\Scripts\activate          # Windows
# ou: source venv/bin/activate  # Linux/macOS

# 3. Instale as dependências
pip install -r requirements.txt

# 4. Configure suas credenciais
# Edite o config.json com usuário/senha do RHiD e token do Telegram

# 5. Execute
python main.py
```

### Rodar em segundo plano (Windows)

Crie um arquivo `iniciar_monitor.bat`:

```bat
@echo off
cd /d "C:\caminho\para\ponto_monitor"
call venv\Scripts\activate
python main.py
```

Para rodar minimizado na inicialização do Windows:
1. Pressione `Win + R` → `shell:startup`
2. Crie um atalho para o `.bat` nessa pasta
3. Nas propriedades do atalho → "Executar: Minimizado"

---

## 📊 Exemplo de Saída no Console

```
============================================================
  Monitor de Ponto — BigCard / RHiD
============================================================
[2026-06-02 11:00:00] INFO     — Sistema iniciado.

[2026-06-02 12:03:00] INFO     — [Ciclo #123] consultando API RHiD...
[2026-06-02 12:03:01] INFO     — Detectado saída almoço funcionário 45 (Maria Souza) às 12:03

============================================================
[2026-06-02 12:03:01] 🔔 SAÍDA ALMOÇO DETECTADA | 45
SAÍDA PARA ALMOÇO DETECTADA
Funcionário: Maria Souza (ID: 45)
Saída: 02/06 12:03
Retorno esperado até: 02/06 13:33
============================================================

[2026-06-02 13:38:00] WARNING  — ALERTA: retorno não realizado funcionário 45 (Maria Souza) — esperado às 13:33

============================================================
[2026-06-02 13:38:00] 🔔 ALERTA RETORNO OVERDUE | 45
ALERTA DE PONTO — RETORNO NÃO REALIZADO
Funcionário: Maria Souza (ID: 45)
Saída almoço: 02/06 12:03
Retorno esperado: 02/06 13:33
O funcionário não retornou do almoço no prazo previsto.
============================================================
```

---

## 🧠 Lógica de Funcionamento

```
Ciclo de polling (a cada 60s)
│
├── Consulta API RHiD → lista de batidas do dia
│
├── Para cada funcionário:
│   ├── Há saída entre 11:00 e 14:00?
│   │   ├── SIM e não está no estado → registra em state.json + notifica Telegram
│   │   └── SIM e está no estado → verificar se já retornou
│   │       ├── Retornou → remove do estado + confirma Telegram
│   │       └── Não retornou → aguarda (será tratado por _check_overdue)
│   └── Sem saída no período → ignora
│
└── _check_overdue():
    └── Para cada registro no estado sem "notified":
        └── Passou do retorno esperado + 5min? → ALERTA + marca notified=true
```

---

## 🛠️ Ajustes na API do RHiD

A API do RHiD pode variar entre versões/contratos. Se os campos da resposta
tiverem nomes diferentes, ajuste o método `_normalize_record()` em `rhid_client.py`.

Campos que o sistema tenta automaticamente:
- `employee_id` / `funcionario_id` / `id_funcionario`
- `employee_name` / `nome` / `name`
- `punches` / `batidas` / `marcacoes`
- Tipo de batida: `ENTRADA`/`E`/`IN` e `SAIDA`/`S`/`OUT`

---

## 🔒 Segurança

- O `config.json` contém senha e token — **não commitar em repositórios públicos**
- Adicione ao `.gitignore`: `config.json`, `state.json`, `*.log`, `venv/`

---

## 📋 Dependências

| Pacote | Versão | Uso |
|---|---|---|
| requests | 2.31.0 | Chamadas HTTP (RHiD + Telegram) |
| python-dateutil | 2.9.0 | Parsing de datas |
