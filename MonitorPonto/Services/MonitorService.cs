using MonitorPonto.Models;

namespace MonitorPonto.Services;

public class MonitorService
{
    private readonly RhidService _rhid;
    private readonly StateService _state;
    private AppConfig _config;
    private CancellationTokenSource? _cts;

    private readonly Dictionary<string, AlertCycleManager> _cycles = new();

    public MonitorService(AppConfig config)
    {
        _config = config;
        _rhid   = new RhidService(config);
        _state  = new StateService();
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

    public void StopAllCycles()
    {
        foreach (var c in _cycles.Values) c.Stop();
        _cycles.Clear();
    }

    // ── Loop principal ───────────────────────────────────────────────────

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (DentroHorarioAtivo())
                {
                    var records = await _rhid.GetPunchRecordsTodayAsync();
                    ProcessRecords(records);
                }
            }
            catch { }

            var sleep = CalcSleep();
            try { await Task.Delay(TimeSpan.FromSeconds(sleep), ct); }
            catch (TaskCanceledException) { break; }
        }
    }

    // ── Horário ativo ────────────────────────────────────────────────────

    private bool DentroHorarioAtivo()
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

    // ── Polling inteligente ──────────────────────────────────────────────

    private int CalcSleep()
    {
        var now   = DateTime.Now;
        var hoje  = DateOnly.FromDateTime(now);
        var jan   = TimeSpan.FromMinutes(_config.Monitor.JanelaProximaMinutos);

        var gatilhos = new List<DateTime>();

        void Add(string horario, int antec, int toler)
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
        if (e.Enabled) Add(e.Horario, e.AntecedenciaMinutos, e.ToleranciaMinutos);

        var s = _config.Schedule.Saida;
        if (s.Enabled) Add(s.Horario, s.AntecedenciaMinutos, s.ToleranciaMinutos);

        var a = _config.Schedule.Almoco;
        if (a.Enabled)
        {
            try
            {
                var fim = hoje.ToDateTime(TimeOnly.Parse(a.JanelaFim))
                    + TimeSpan.FromMinutes(a.DuracaoMinutos);
                gatilhos.Add(fim - TimeSpan.FromMinutes(a.AntecedenciaMinutos));
                gatilhos.Add(fim + TimeSpan.FromMinutes(a.ToleranciaMinutos));
            }
            catch { }
        }

        foreach (var g in gatilhos)
            if (Math.Abs((g - now).TotalSeconds) <= jan.TotalSeconds)
                return _config.Monitor.PollingProximoSeconds;

        return _config.Monitor.PollingIntervalSeconds;
    }

    // ── Processar batidas ────────────────────────────────────────────────

    private void ProcessRecords(List<PunchRecord> records)
    {
        var idPerson = _config.Person.IdPerson.ToString();
        var record   = records.FirstOrDefault(r => r.EmployeeId == idPerson);
        var hoje     = DateTime.Now.ToString("yyyy-MM-dd");
        var punches  = new List<(DateTime Ts, string Type)>();

        if (record != null)
        {
            foreach (var p in record.Punches)
                if (DateTime.TryParse($"{record.Date} {p.Time}", out var ts))
                    punches.Add((ts, p.Type));
            punches = punches.OrderBy(x => x.Ts).ToList();
        }

        // Separa por tipo real (ignora duplicidades do mesmo tipo consecutivo)
        var entradas = punches.Where(p => p.Type == "ENTRADA").ToList();
        var saidas   = punches.Where(p => p.Type == "SAIDA").ToList();

        CheckEntrada(entradas);
        CheckAlmoco(entradas, saidas, hoje);
        CheckSaida(entradas, saidas);
    }

    // ── ENTRADA ──────────────────────────────────────────────────────────

    private void CheckEntrada(List<(DateTime Ts, string Type)> entradas)
    {
        var cfg = _config.Schedule.Entrada;
        if (!cfg.Enabled) return;

        var now   = DateTime.Now;
        var hoje  = now.Date;
        var keyA  = $"{_config.Person.IdPerson}_entrada_aviso_{hoje:yyyyMMdd}";
        var keyU  = $"{_config.Person.IdPerson}_entrada_urgente_{hoje:yyyyMMdd}";

        // Já tem pelo menos 1 entrada real → não alerta
        if (entradas.Count >= 1) return;
        var punches = entradas; // compatibilidade

        if (!TimeOnly.TryParse(cfg.Horario, out var t)) return;
        var base_     = hoje.Add(t.ToTimeSpan());
        var gatAviso  = base_ - TimeSpan.FromMinutes(cfg.AntecedenciaMinutos);
        var gatUrg    = base_ + TimeSpan.FromMinutes(cfg.ToleranciaMinutos);
        var nome      = _config.Person.Nome;
        var horFmt    = t.ToString("HH:mm");

        if (now >= gatUrg && !_state.Exists(keyU))
        {
            _state.Set(keyU, now.ToString("o"));
            FireAlert(AlertTipo.Entrada, AlertNivel.Urgente, nome, horFmt, "");
            return;
        }
        if (now >= gatAviso && !_state.Exists(keyA))
        {
            _state.Set(keyA, now.ToString("o"));
            FireAlert(AlertTipo.Entrada, AlertNivel.Aviso, nome, horFmt, "");
        }
    }

    // ── ALMOÇO ───────────────────────────────────────────────────────────

    private void CheckAlmoco(
        List<(DateTime Ts, string Type)> entradas,
        List<(DateTime Ts, string Type)> saidas,
        string date)
    {
        var cfg = _config.Schedule.Almoco;
        if (!cfg.Enabled) return;

        var id     = _config.Person.IdPerson;
        var nome   = _config.Person.Nome;
        var hoje   = DateTime.Now.Date;
        var now    = DateTime.Now;
        var keyEst = $"{id}_almoco";

        // Retorno do almoço = 2ª entrada real após 1ª saída real
        // (ignora entradas duplicadas consecutivas)
        if (entradas.Count >= 2 && saidas.Count >= 1)
        {
            // Tem saída almoço E retorno → cancela alerta
            var primeiraS = saidas[0];
            var segundaE  = entradas.FirstOrDefault(e => e.Ts > primeiraS.Ts);
            if (segundaE.Ts != default)
            {
                _state.Remove(keyEst);
                _state.Remove($"{id}_almoco_aviso_{hoje:yyyyMMdd}");
                _state.Remove($"{id}_almoco_urgente_{hoje:yyyyMMdd}");
                StopCycle($"{AlertTipo.Almoco}_{AlertNivel.Aviso}");
                StopCycle($"{AlertTipo.Almoco}_{AlertNivel.Urgente}");
                return;
            }
        }

        // Saída para almoço = 1ª saída real dentro da janela de almoço
        if (saidas.Count >= 1)
        {
            var primeiraS = saidas[0];
            if (TimeOnly.TryParse(cfg.JanelaInicio, out var jIni) &&
                TimeOnly.TryParse(cfg.JanelaFim,    out var jFim))
            {
                var hs = TimeOnly.FromDateTime(primeiraS.Ts);
                if (hs >= jIni && hs <= jFim && !_state.Exists(keyEst))
                {
                    var esperado = primeiraS.Ts + TimeSpan.FromMinutes(cfg.DuracaoMinutos);
                    var gA = esperado - TimeSpan.FromMinutes(cfg.AntecedenciaMinutos);
                    var gU = esperado + TimeSpan.FromMinutes(cfg.ToleranciaMinutos);
                    _state.Set(keyEst, new
                    {
                        lunch_start     = primeiraS.Ts.ToString("o"),
                        expected_return = esperado.ToString("o"),
                        gatilho_aviso   = gA.ToString("o"),
                        gatilho_urgente = gU.ToString("o"),
                    });
                }
            }
        }

        // Verificar alertas
        var rec = _state.Get<System.Text.Json.Nodes.JsonObject>(keyEst);
        if (rec == null) return;

        var keyA = $"{id}_almoco_aviso_{hoje:yyyyMMdd}";
        var keyU = $"{id}_almoco_urgente_{hoje:yyyyMMdd}";

        if (!DateTime.TryParse(rec["lunch_start"]?.ToString(),     out var ls))  return;
        if (!DateTime.TryParse(rec["expected_return"]?.ToString(),  out var er))  return;
        if (!DateTime.TryParse(rec["gatilho_aviso"]?.ToString(),    out var gAv)) return;
        if (!DateTime.TryParse(rec["gatilho_urgente"]?.ToString(),  out var gUr)) return;

        var saidaFmt   = ls.ToString("HH:mm");
        var esperadoFmt = er.ToString("HH:mm");

        if (now >= gUr && !_state.Exists(keyU))
        {
            _state.Set(keyU, now.ToString("o"));
            FireAlert(AlertTipo.Almoco, AlertNivel.Urgente, nome, saidaFmt, esperadoFmt);
            return;
        }
        if (now >= gAv && !_state.Exists(keyA))
        {
            _state.Set(keyA, now.ToString("o"));
            FireAlert(AlertTipo.Almoco, AlertNivel.Aviso, nome, saidaFmt, esperadoFmt);
        }
    }

    // ── SAÍDA ────────────────────────────────────────────────────────────

    private void CheckSaida(
        List<(DateTime Ts, string Type)> entradas,
        List<(DateTime Ts, string Type)> saidas)
    {
        var cfg = _config.Schedule.Saida;
        if (!cfg.Enabled) return;

        var now   = DateTime.Now;
        var hoje  = now.Date;
        var keyA  = $"{_config.Person.IdPerson}_saida_aviso_{hoje:yyyyMMdd}";
        var keyU  = $"{_config.Person.IdPerson}_saida_urgente_{hoje:yyyyMMdd}";

        // Saída final = última batida é do tipo Saída e não tem entrada depois dela
        // Considera saída final quando há mais saídas que entradas pós-almoço
        // Simplificado: se a última batida registrada é SAIDA → já bateu saída
        var ultimaBatida = entradas.Concat(saidas).OrderBy(x => x.Ts).LastOrDefault();
        var temSaidaFinal = ultimaBatida.Type == "SAIDA"
            && TimeOnly.FromDateTime(ultimaBatida.Ts) >=
               TimeOnly.Parse(_config.Schedule.Almoco.JanelaFim);
        if (temSaidaFinal) return;

        var punches = saidas; // compatibilidade com código abaixo

        if (!TimeOnly.TryParse(cfg.Horario, out var t)) return;
        var base_    = hoje.Add(t.ToTimeSpan());
        var gatAviso = base_ - TimeSpan.FromMinutes(cfg.AntecedenciaMinutos);
        var gatUrg   = base_ + TimeSpan.FromMinutes(cfg.ToleranciaMinutos);
        var nome     = _config.Person.Nome;
        var horFmt   = t.ToString("HH:mm");

        if (now >= gatUrg && !_state.Exists(keyU))
        {
            _state.Set(keyU, now.ToString("o"));
            FireAlert(AlertTipo.Saida, AlertNivel.Urgente, nome, horFmt, "");
            return;
        }
        if (now >= gatAviso && !_state.Exists(keyA))
        {
            _state.Set(keyA, now.ToString("o"));
            FireAlert(AlertTipo.Saida, AlertNivel.Aviso, nome, horFmt, "");
        }
    }

    // ── AlertCycleManager ────────────────────────────────────────────────

    private void FireAlert(AlertTipo tipo, AlertNivel nivel,
        string nome, string info1, string info2)
    {
        var key = $"{tipo}_{nivel}";
        if (_cycles.ContainsKey(key)) return;

        var manager = new AlertCycleManager(tipo, nivel, nome, info1, info2);
        _cycles[key] = manager;
        manager.Start();
    }

    private void StopCycle(string key)
    {
        if (_cycles.TryGetValue(key, out var c))
        {
            c.Stop();
            _cycles.Remove(key);
        }
    }
}
