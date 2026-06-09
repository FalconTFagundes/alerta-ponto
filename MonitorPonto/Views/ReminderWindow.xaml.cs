using System.Windows;
using System.Windows.Threading;

namespace MonitorPonto.Views;

public partial class ReminderWindow : Window
{
    private DispatcherTimer? _clockTimer;

    public ReminderWindow(string icone, string mensagem, string horario, string proximoAlerta)
    {
        InitializeComponent();
        TxtIcon.Text     = icone;
        TxtMensagem.Text = mensagem;
        TxtHorario.Text  = horario;
        TxtProximo.Text  = proximoAlerta;

        // Atualiza o countdown do próximo alerta
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => AtualizarContagem();
        _clockTimer.Start();
    }

    private DateTime _proximoAlertaAt;

    public void SetProximoAlerta(DateTime when)
    {
        _proximoAlertaAt = when;
        AtualizarContagem();
    }

    private void AtualizarContagem()
    {
        if (_proximoAlertaAt == default) return;
        var restante = _proximoAlertaAt - DateTime.Now;
        if (restante.TotalSeconds > 0)
            TxtProximo.Text = $"🔔 Próximo alerta em {(int)restante.TotalMinutes}:{restante.Seconds:D2}";
        else
            TxtProximo.Text = "🔔 Próximo alerta agora...";
    }

    private void BtnFechar_Click(object sender, RoutedEventArgs e)
    {
        _clockTimer?.Stop();
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _clockTimer?.Stop();
        base.OnClosing(e);
    }
}
