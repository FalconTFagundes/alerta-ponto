"""
teste_alerta.py
Testa os 6 tipos de alerta: aviso e urgente para cada evento.
Roda: python teste_alerta.py
"""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent))
from alerter import show_alert

print("1/6 - Almoço AVISO...")
show_alert("Rafael de Souza Fagundes", tipo="ALMOCO", urgente=False, saida="12:00", esperado="13:30")

print("2/6 - Almoço URGENTE...")
show_alert("Rafael de Souza Fagundes", tipo="ALMOCO", urgente=True, saida="12:00", esperado="13:30")

print("3/6 - Entrada AVISO...")
show_alert("Rafael de Souza Fagundes", tipo="ENTRADA", urgente=False, horario="08:00")

print("4/6 - Entrada URGENTE...")
show_alert("Rafael de Souza Fagundes", tipo="ENTRADA", urgente=True, horario="08:00")

print("5/6 - Saída AVISO...")
show_alert("Rafael de Souza Fagundes", tipo="SAIDA", urgente=False, horario="18:00")

print("6/6 - Saída URGENTE...")
show_alert("Rafael de Souza Fagundes", tipo="SAIDA", urgente=True, horario="18:00")

print("Concluído.")
