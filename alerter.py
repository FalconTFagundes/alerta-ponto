"""
alerter.py
Janela fullscreen com som — Windows only.
Dois níveis: AVISO (1º alerta) e URGENTE (2º alerta após tolerância).
"""

import logging
import threading
import tkinter as tk
import winsound
from datetime import datetime

logger = logging.getLogger("ponto_monitor.alerter")

THEMES = {
    "ALMOCO": {
        "bg": "#1a1a2e", "accent": "#e94560",
        "icon": "⏰", "titulo": "HORA DE RETORNAR DO ALMOÇO!",
        "subtitulo": "Você não retornou do almoço no horário.",
        "btn_text": "✅  JÁ FUI BATER O PONTO",
    },
    "ENTRADA": {
        "bg": "#0f3460", "accent": "#f5a623",
        "icon": "🌅", "titulo": "HORA DE BATER A ENTRADA!",
        "subtitulo": "Você ainda não bateu o ponto de entrada.",
        "btn_text": "✅  JÁ FUI BATER O PONTO",
    },
    "SAIDA": {
        "bg": "#1b4332", "accent": "#52b788",
        "icon": "🌆", "titulo": "HORA DE BATER A SAÍDA!",
        "subtitulo": "Não esqueça de bater o ponto de saída.",
        "btn_text": "✅  JÁ FUI BATER O PONTO",
    },
}

URGENTE_OVERLAY = {
    "bg": "#3d0000", "accent": "#ff1a1a",
    "icon": "🚨", "titulo": "VOCÊ AINDA NÃO BATEU O PONTO!!",
    "subtitulo": "O prazo já passou! Vá bater o ponto agora.",
    "btn_text": "✅  JÁ FUI BATER O PONTO",
}


def _sound_aviso(stop: threading.Event) -> None:
    """Som de exclamação padrão — aviso normal."""
    while not stop.is_set():
        try:
            winsound.MessageBeep(winsound.MB_ICONEXCLAMATION)
        except Exception:
            pass
        stop.wait(timeout=3)


def _sound_urgente(stop: threading.Event) -> None:
    """Som crítico do Windows — mais estrondoso, intervalo menor."""
    while not stop.is_set():
        try:
            winsound.MessageBeep(winsound.MB_ICONHAND)
            stop.wait(timeout=0.5)
            winsound.MessageBeep(winsound.MB_ICONHAND)
            stop.wait(timeout=0.5)
            winsound.MessageBeep(winsound.MB_ICONHAND)
        except Exception:
            pass
        stop.wait(timeout=2)


def show_alert(
    nome: str,
    tipo: str = "ALMOCO",
    urgente: bool = False,
    saida: str = "",
    esperado: str = "",
    horario: str = "",
) -> None:
    """
    nome    : nome do funcionário
    tipo    : ALMOCO | ENTRADA | SAIDA
    urgente : False = 1º alerta (aviso), True = 2º alerta (tolerância estourou)
    """
    theme = URGENTE_OVERLAY if urgente else THEMES.get(tipo, THEMES["ALMOCO"])
    nivel = "URGENTE" if urgente else "AVISO"
    logger.info(f"Exibindo alerta [{tipo}][{nivel}] para {nome}")

    stop_sound = threading.Event()
    fn_sound   = _sound_urgente if urgente else _sound_aviso
    threading.Thread(target=fn_sound, args=(stop_sound,), daemon=True).start()

    def _build():
        root = tk.Tk()
        root.title(f"{'🚨' if urgente else '⏰'} {theme['titulo']}")
        root.attributes("-fullscreen", True)
        root.attributes("-topmost", True)
        root.configure(bg=theme["bg"])
        root.protocol("WM_DELETE_WINDOW", lambda: None)

        frame = tk.Frame(root, bg=theme["bg"])
        frame.place(relx=0.5, rely=0.5, anchor="center")

        tk.Label(frame, text=theme["icon"],
                 font=("Segoe UI Emoji", 80),
                 bg=theme["bg"], fg="white").pack(pady=(0, 10))

        tk.Label(frame, text=theme["titulo"],
                 font=("Segoe UI", 34, "bold"),
                 bg=theme["bg"], fg=theme["accent"]).pack(pady=(0, 8))

        tk.Label(frame, text=theme["subtitulo"],
                 font=("Segoe UI", 16),
                 bg=theme["bg"], fg="#cccccc").pack(pady=(0, 28))

        tk.Label(frame, text=f"👤  {nome}",
                 font=("Segoe UI", 20),
                 bg=theme["bg"], fg="white").pack(pady=4)

        if tipo == "ALMOCO" and saida and esperado:
            tk.Label(frame, text=f"🍽️  Saída para almoço: {saida}",
                     font=("Segoe UI", 17), bg=theme["bg"], fg="#a8dadc").pack(pady=3)
            tk.Label(frame, text=f"🔔  Retorno esperado: {esperado}",
                     font=("Segoe UI", 17), bg=theme["bg"], fg="#a8dadc").pack(pady=3)
        elif horario:
            tk.Label(frame, text=f"🕐  Horário previsto: {horario}",
                     font=("Segoe UI", 17), bg=theme["bg"], fg="#a8dadc").pack(pady=3)

        tk.Label(frame, text=f"🕐  Agora: {datetime.now().strftime('%H:%M')}",
                 font=("Segoe UI", 17), bg=theme["bg"], fg="#f4a261").pack(pady=(3, 35))

        def on_ok():
            stop_sound.set()
            root.destroy()
            logger.info(f"Alerta [{tipo}][{nivel}] fechado pelo usuário")

        btn = tk.Button(
            frame, text=theme["btn_text"],
            font=("Segoe UI", 20, "bold"),
            bg=theme["accent"], fg="white",
            activebackground=theme["bg"], activeforeground=theme["accent"],
            padx=40, pady=18, relief="flat", cursor="hand2",
            command=on_ok,
        )
        btn.pack()

        def pulse(on=True):
            if not root.winfo_exists():
                return
            btn.configure(bg=theme["accent"] if on else theme["bg"])
            interval = 400 if urgente else 700
            root.after(interval, lambda: pulse(not on))

        pulse()
        root.mainloop()

    t = threading.Thread(target=_build, daemon=False)
    t.start()
    t.join()
    stop_sound.set()
