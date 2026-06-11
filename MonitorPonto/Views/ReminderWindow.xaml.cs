using System.Windows;
using System.Windows.Threading;

namespace MonitorPonto.Views;

public partial class ReminderWindow : Window
{
    private DispatcherTimer? _timer;
    private DateTime _proximoAlertaAt;

    public ReminderWindow(string icone, string mensagem, string horario, string _)
    {
        InitializeComponent();
        TxtIcon.Text     = icone;
        TxtMensagem.Text = mensagem;
        TxtHorario.Text  = horario;
    }

    public void SetProximoAlerta(DateTime when)
    {
        _proximoAlertaAt = when;
        AtualizarContagem();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => AtualizarContagem();
        _timer.Start();
    }

    private void AtualizarContagem()
    {
        var restante = _proximoAlertaAt - DateTime.Now;
        TxtProximo.Text = restante.TotalSeconds > 0
            ? $"🔔 Próximo alerta em {(int)restante.TotalMinutes}:{restante.Seconds:D2}"
            : "🔔 Próximo alerta agora...";
    }

    private void BtnFechar_Click(object sender, RoutedEventArgs e)
    {
        _timer?.Stop();
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _timer?.Stop();
        base.OnClosing(e);
    }
}
