using MonitorPonto.Models;

namespace MonitorPonto.Services;

public class MonitorService
{
    private readonly RhidService _rhid;
    private readonly StateService _state;
    private AppConfig _config;
    private CancellationTokenSource? _cts;

    public event Action<AlertTipo, AlertNivel, string, string, string>? AlertRequested;
    // tipo, nivel, nome, info1 (saida/horario), info2 (esperado)

    public MonitorService(AppConfig config)
    {
        _config = config;
        _rhid   = new RhidService(config);
        _state  = new StateService();
    }

    public void UpdateConfig(AppConfig config)
    {
        _config = config;
        _rhid.UpdateConfig(config);
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => LoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    // ── Loop principal ──────────────────────────────────────────────────

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (DentroDoHorarioAtivo())
                {
                    var records = await _rhid.GetPunchRecordsTodayAsync();
                    ProcessRecords(records);
                }
            }
            catch { }

            var sleep = CalcSleep();
            await Task.Delay(TimeSpan.FromSeconds(sleep), ct).ContinueWith(_ => { });
        }
    }

    // ── Horário ativo ───────────────────────────────────────────────────

    private bool DentroDoHorarioAtivo()
    {
        try
        {
            var now   = TimeOnly.FromDateTime(DateTime.Now);
            var start = TimeOnly.Parse(_config.Schedule.HorarioAtivo.Inicio);
            var end   = TimeOnly.Parse(_config.Schedule.HorarioAtivo.Fim);
            return now >= start && now <= end;
        }
        catch { return true; }
    }

    // ── Polling inteligente ─────────────────────────────────────────────

    private int CalcSleep()
    {
        var now    = DateTime.Now;
        var hoje   = DateOnly.FromDateTime(now);
        var janela = TimeSpan.FromMinutes(_config.Monitor.JanelaProximaMinutos);

        var gatilhos = new List<DateTime>();

        void AddGatilhos(string horario, int antec, int toler)
        {
            try
            {
                var base_ = hoje.ToDateTime(TimeOnly.Parse(horario));
                gatilhos.Add(base_ - TimeSpan.FromMinutes(antec));
                gatilhos.Add(base_ + TimeSpan.FromMinutes(toler));
            }
            catch { }
        }

        var e = _config.Schedule.Entrada;
        if (e.Enabled) AddGatilhos(e.Horario, e.AntecedenciaMinutos, e.ToleranciaMinutos);

        var s = _config.Schedule.Saida;
        if (s.Enabled) AddGatilhos(s.Horario, s.AntecedenciaMinutos, s.ToleranciaMinutos);

        var a = _config.Schedule.Almoco;
        if (a.Enabled)
        {
            try
            {
                var fimJanela = hoje.ToDateTime(TimeOnly.Parse(a.JanelaFim));
                var baseAlm   = fimJanela + TimeSpan.FromMinutes(a.DuracaoMinutos);
                gatilhos.Add(baseAlm - TimeSpan.FromMinutes(a.AntecedenciaMinutos));
                gatilhos.Add(baseAlm + TimeSpan.FromMinutes(a.ToleranciaMinutos));
            }
            catch { }
        }

        foreach (var g in gatilhos)
        {
            if (Math.Abs((g - now).TotalSeconds) <= janela.TotalSeconds)
                return _config.Monitor.PollingProximoSeconds;
        }

        return _config.Monitor.PollingIntervalSeconds;
    }

    // ── Processar batidas ───────────────────────────────────────────────

    private void ProcessRecords(List<PunchRecord> records)
    {
        var idPerson = _config.Person.IdPerson.ToString();
        var record   = records.FirstOrDefault(r => r.EmployeeId == idPerson);

        var hoje   = DateTime.Now.ToString("yyyy-MM-dd");
        var punches = new List<(DateTime Ts, string Type)>();

        if (record != null)
        {
            foreach (var p in record.Punches)
            {
                if (DateTime.TryParse($"{record.Date} {p.Time}", out var ts))
                    punches.Add((ts, p.Type));
            }
            punches = punches.OrderBy(x => x.Ts).ToList();
        }

        CheckEntrada(punches);
        CheckAlmoco(punches, hoje);
        CheckSaida(punches);
    }

    // ── ENTRADA ─────────────────────────────────────────────────────────

    private void CheckEntrada(List<(DateTime Ts, string Type)> punches)
    {
        var cfg = _config.Schedule.Entrada;
        if (!cfg.Enabled) return;

        var id    = _config.Person.IdPerson;
        var nome  = _config.Person.Nome;
        var hoje  = DateTime.Now.Date;
        var now   = DateTime.Now;

        var keyAviso  = $"{id}_entrada_aviso_{hoje:yyyyMMdd}";
        var keyUrgent = $"{id}_entrada_urgente_{hoje:yyyyMMdd}";

        var temEntrada = punches.Count >= 1;
        if (temEntrada) return;

        if (!TimeOnly.TryParse(cfg.Horario, out var horarioTime)) return;
        var horarioDt     = hoje.Add(horarioTime.ToTimeSpan());
        var gatilhoAviso  = horarioDt - TimeSpan.FromMinutes(cfg.AntecedenciaMinutos);
        var gatilhoUrgent = horarioDt + TimeSpan.FromMinutes(cfg.ToleranciaMinutos);
        var horarioFmt    = horarioTime.ToString("HH:mm");

        if (now >= gatilhoUrgent && !_state.Exists(keyUrgent))
        {
            _state.Set(keyUrgent, now.ToString("o"));
            AlertRequested?.Invoke(AlertTipo.Entrada, AlertNivel.Urgente, nome, horarioFmt, "");
            return;
        }

        if (now >= gatilhoAviso && !_state.Exists(keyAviso))
        {
            _state.Set(keyAviso, now.ToString("o"));
            AlertRequested?.Invoke(AlertTipo.Entrada, AlertNivel.Aviso, nome, horarioFmt, "");
        }
    }

    // ── ALMOÇO ──────────────────────────────────────────────────────────

    private void CheckAlmoco(List<(DateTime Ts, string Type)> punches, string date)
    {
        var cfg  = _config.Schedule.Almoco;
        if (!cfg.Enabled) return;

        var id   = _config.Person.IdPerson;
        var nome = _config.Person.Nome;
        var hoje = DateTime.Now.Date;
        var now  = DateTime.Now;
        var keyEstado = $"{id}_almoco";

        // 3ª batida = retorno
        if (punches.Count >= 3)
        {
            _state.Remove(keyEstado);
            _state.Remove($"{id}_almoco_aviso_{hoje:yyyyMMdd}");
            _state.Remove($"{id}_almoco_urgente_{hoje:yyyyMMdd}");
            return;
        }

        // 2ª batida = saída almoço
        if (punches.Count >= 2)
        {
            var segunda = punches[1];

            if (TimeOnly.TryParse(cfg.JanelaInicio, out var jIni) &&
                TimeOnly.TryParse(cfg.JanelaFim,    out var jFim))
            {
                var hora2 = TimeOnly.FromDateTime(segunda.Ts);
                if (hora2 >= jIni && hora2 <= jFim)
                {
                    if (!_state.Exists(keyEstado))
                    {
                        var esperado  = segunda.Ts + TimeSpan.FromMinutes(cfg.DuracaoMinutos);
                        var gAviso    = esperado - TimeSpan.FromMinutes(cfg.AntecedenciaMinutos);
                        var gUrgente  = esperado + TimeSpan.FromMinutes(cfg.ToleranciaMinutos);

                        _state.Set(keyEstado, new
                        {
                            lunch_start     = segunda.Ts.ToString("o"),
                            expected_return = esperado.ToString("o"),
                            gatilho_aviso   = gAviso.ToString("o"),
                            gatilho_urgente = gUrgente.ToString("o"),
                        });
                    }
                }
            }
        }

        // Verificar alertas
        var rec = _state.Get<System.Text.Json.Nodes.JsonObject>(keyEstado);
        if (rec == null) return;

        var keyAviso   = $"{id}_almoco_aviso_{hoje:yyyyMMdd}";
        var keyUrgente = $"{id}_almoco_urgente_{hoje:yyyyMMdd}";

        if (!DateTime.TryParse(rec["lunch_start"]?.ToString(),     out var lunchStart))    return;
        if (!DateTime.TryParse(rec["expected_return"]?.ToString(),  out var expectedReturn)) return;
        if (!DateTime.TryParse(rec["gatilho_aviso"]?.ToString(),    out var gA))             return;
        if (!DateTime.TryParse(rec["gatilho_urgente"]?.ToString(),  out var gU))             return;

        var saidaFmt   = lunchStart.ToString("HH:mm");
        var esperadoFmt = expectedReturn.ToString("HH:mm");

        if (now >= gU && !_state.Exists(keyUrgente))
        {
            _state.Set(keyUrgente, now.ToString("o"));
            AlertRequested?.Invoke(AlertTipo.Almoco, AlertNivel.Urgente, nome, saidaFmt, esperadoFmt);
            return;
        }

        if (now >= gA && !_state.Exists(keyAviso))
        {
            _state.Set(keyAviso, now.ToString("o"));
            AlertRequested?.Invoke(AlertTipo.Almoco, AlertNivel.Aviso, nome, saidaFmt, esperadoFmt);
        }
    }

    // ── SAÍDA ────────────────────────────────────────────────────────────

    private void CheckSaida(List<(DateTime Ts, string Type)> punches)
    {
        var cfg  = _config.Schedule.Saida;
        if (!cfg.Enabled) return;

        var id   = _config.Person.IdPerson;
        var nome = _config.Person.Nome;
        var hoje = DateTime.Now.Date;
        var now  = DateTime.Now;

        var keyAviso   = $"{id}_saida_aviso_{hoje:yyyyMMdd}";
        var keyUrgente = $"{id}_saida_urgente_{hoje:yyyyMMdd}";

        var temSaidaFinal = punches.Count > 0 && punches.Count % 2 == 0;
        if (temSaidaFinal) return;

        if (!TimeOnly.TryParse(cfg.Horario, out var horarioTime)) return;
        var horarioDt    = hoje.Add(horarioTime.ToTimeSpan());
        var gatilhoAviso = horarioDt - TimeSpan.FromMinutes(cfg.AntecedenciaMinutos);
        var gatilhoUrg   = horarioDt + TimeSpan.FromMinutes(cfg.ToleranciaMinutos);
        var horarioFmt   = horarioTime.ToString("HH:mm");

        if (now >= gatilhoUrg && !_state.Exists(keyUrgente))
        {
            _state.Set(keyUrgente, now.ToString("o"));
            AlertRequested?.Invoke(AlertTipo.Saida, AlertNivel.Urgente, nome, horarioFmt, "");
            return;
        }

        if (now >= gatilhoAviso && !_state.Exists(keyAviso))
        {
            _state.Set(keyAviso, now.ToString("o"));
            AlertRequested?.Invoke(AlertTipo.Saida, AlertNivel.Aviso, nome, horarioFmt, "");
        }
    }
}
