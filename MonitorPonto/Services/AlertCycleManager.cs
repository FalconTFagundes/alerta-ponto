using System.Windows;
using MonitorPonto.Models;
using MonitorPonto.Views;

namespace MonitorPonto.Services;

/// <summary>
/// Ciclo completo de alertas:
///   1. Fullscreen por 40s
///   2. Se não confirmado → ReminderWindow central
///   3. Aguarda 5 min → repete
///   4. Quando ponto batido → para
/// </summary>
public class AlertCycleManager
{
    private readonly AlertTipo  _tipo;
    private readonly AlertNivel _nivel;
    private readonly string _nome;
    private readonly string _info1;
    private readonly string _info2;

    private CancellationTokenSource? _cts;
    private ReminderWindow? _reminder;
    private bool _running;

    private const int CycleIntervalMinutes = 3;

    public AlertCycleManager(AlertTipo tipo, AlertNivel nivel,
        string nome, string info1, string info2)
    {
        _tipo  = tipo;
        _nivel = nivel;
        _nome  = nome;
        _info1 = info1;
        _info2 = info2;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _cts = new CancellationTokenSource();
        Task.Run(() => CycleLoop(_cts.Token));
    }

    public void Stop()
    {
        _running = false;
        _cts?.Cancel();
        CloseReminder();
    }

    private async Task CycleLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // 1. Fullscreen 40s
            bool confirmed = await ShowFullscreenAsync(ct);
            if (ct.IsCancellationRequested || confirmed) break;

            // 2. Reminder central
            var proximoAlerta = DateTime.Now.AddMinutes(CycleIntervalMinutes);
            ShowReminder(proximoAlerta);

            // 3. Aguarda 5 min
            try { await Task.Delay(TimeSpan.FromMinutes(CycleIntervalMinutes), ct); }
            catch (TaskCanceledException) { break; }

            // 4. Fecha reminder e repete
            CloseReminder();
        }

        CloseReminder();
        _running = false;
    }

    private Task<bool> ShowFullscreenAsync(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>();

        Application.Current.Dispatcher.Invoke(() =>
        {
            if (ct.IsCancellationRequested) { tcs.TrySetResult(false); return; }

            var win = new AlertWindow(_tipo, _nivel, _nome, _info1, _info2);
            win.Closed += (_, _) => tcs.TrySetResult(win.ClosedByUser);

            ct.Register(() =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try { win.Close(); } catch { }
                });
                tcs.TrySetResult(false);
            });

            win.Show();
            win.Activate();
        });

        return tcs.Task;
    }

    private void ShowReminder(DateTime proximoAlerta)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            CloseReminder();

            var (icone, mensagem) = _tipo switch
            {
                AlertTipo.Entrada => ("🌅", "Você não bateu o ponto de entrada!"),
                AlertTipo.Almoco  => ("⏰", "Você não retornou do almoço!"),
                AlertTipo.Saida   => ("🌆", "Você não bateu o ponto de saída!"),
                _                 => ("⚠️", "Você não bateu o ponto!")
            };

            _reminder = new ReminderWindow(icone, mensagem,
                $"Horário: {DateTime.Now:HH:mm}", "");
            _reminder.SetProximoAlerta(proximoAlerta);
            _reminder.Show();
        });
    }

    private void CloseReminder()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            try { _reminder?.Close(); } catch { }
            _reminder = null;
        });
    }
}
