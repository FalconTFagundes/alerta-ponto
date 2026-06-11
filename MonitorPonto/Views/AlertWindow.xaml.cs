using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MonitorPonto.Models;
using MonitorPonto.Services;

namespace MonitorPonto.Views;

public partial class AlertWindow : Window
{
    private DispatcherTimer? _soundTimer;
    private DispatcherTimer? _clockTimer;
    private DispatcherTimer? _countdownTimer;
    private DispatcherTimer? _autoCloseTimer;
    private bool _urgente;
    private int _secondsLeft;

    public bool ClosedByUser { get; private set; } = false;

    private const int AutoCloseSecs = 40;

    public AlertWindow(AlertTipo tipo, AlertNivel nivel,
        string nome, string info1, string info2)
    {
        InitializeComponent();
        _urgente     = nivel == AlertNivel.Urgente;
        _secondsLeft = AutoCloseSecs;
        ConfigurarVisual(tipo, nivel, nome, info1, info2);
        Loaded += (_, _) =>
        {
            StartSound();
            StartClock();
            StartCountdown();
            BtnOk.Focus();
        };
    }

    // ── Visual ───────────────────────────────────────────────────────────

    private void ConfigurarVisual(AlertTipo tipo, AlertNivel nivel,
        string nome, string info1, string info2)
    {
        string bg, accent, icon, titulo, subtitulo, btnText;

        if (nivel == AlertNivel.Urgente)
        {
            bg        = "#3d0000";
            accent    = "#FF1A1A";
            icon      = "🚨";
            titulo    = "VOCÊ AINDA NÃO BATEU O PONTO!!";
            subtitulo = "O prazo já passou! Vá bater o ponto agora.";
            btnText   = "✅  JÁ FUI BATER O PONTO";
        }
        else
        {
            (bg, accent, icon, titulo, subtitulo, btnText) = tipo switch
            {
                AlertTipo.Entrada => ("#0f3460", "#F5A623", "🌅",
                    "HORA DE BATER A ENTRADA!",
                    "Você ainda não bateu o ponto de entrada.",
                    "✅  JÁ FUI BATER O PONTO"),
                AlertTipo.Almoco  => ("#1a1a2e", "#E94560", "⏰",
                    "HORA DE RETORNAR DO ALMOÇO!",
                    "Você não retornou do almoço no horário.",
                    "✅  JÁ FUI BATER O PONTO"),
                AlertTipo.Saida   => ("#1b4332", "#52B788", "🌆",
                    "HORA DE BATER A SAÍDA!",
                    "Não esqueça de bater o ponto de saída.",
                    "✅  JÁ FUI BATER O PONTO"),
                _                 => ("#1a1a2e", "#E94560", "⏰", "ATENÇÃO!", "", "✅  OK")
            };
        }

        GridRoot.Background  = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
        TxtIcon.Text         = icon;
        TxtTitulo.Text       = titulo;
        TxtTitulo.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accent));
        TxtSubtitulo.Text    = subtitulo;
        TxtNome.Text         = $"👤  {nome}";
        BtnOk.Content        = btnText;

        var accentColor = (Color)ColorConverter.ConvertFromString(accent);
        var bgColor     = (Color)ColorConverter.ConvertFromString(bg);
        var brush       = new SolidColorBrush(accentColor);
        BtnOk.Background = brush;

        var anim = new ColorAnimation(accentColor, bgColor,
            new Duration(TimeSpan.FromMilliseconds(_urgente ? 350 : 650)))
        {
            AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever
        };
        brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);

        if (tipo == AlertTipo.Almoco && !string.IsNullOrEmpty(info1))
        {
            TxtInfo1.Text = $"🍽️  Saída para almoço: {info1}";
            TxtInfo2.Text = $"🔔  Retorno esperado: {info2}";
        }
        else if (!string.IsNullOrEmpty(info1))
        {
            TxtInfo1.Text = $"🕐  Horário previsto: {info1}";
            TxtInfo2.Text = "";
        }
    }

    // ── Relógio ──────────────────────────────────────────────────────────

    private void StartClock()
    {
        TxtAgora.Text = $"🕐  Agora: {DateTime.Now:HH:mm}";
        _clockTimer   = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _clockTimer.Tick += (_, _) => TxtAgora.Text = $"🕐  Agora: {DateTime.Now:HH:mm}";
        _clockTimer.Start();
    }

    // ── Countdown ────────────────────────────────────────────────────────

    private void StartCountdown()
    {
        AtualizarCountdown();
        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) =>
        {
            _secondsLeft--;
            AtualizarCountdown();
        };
        _countdownTimer.Start();

        // Auto-close após AutoCloseSecs
        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(AutoCloseSecs)
        };
        _autoCloseTimer.Tick += (_, _) =>
        {
            _autoCloseTimer.Stop();
            Fechar(byUser: false);
        };
        _autoCloseTimer.Start();
    }

    private void AtualizarCountdown()
    {
        TxtCountdown.Text = _secondsLeft > 0
            ? $"Esta janela fechará em {_secondsLeft}s"
            : "Fechando...";
    }

    // ── Som ──────────────────────────────────────────────────────────────

    private void StartSound()
    {
        TocarSom();
        _soundTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_urgente ? 2 : 3)
        };
        _soundTimer.Tick += (_, _) => TocarSom();
        _soundTimer.Start();
    }

    private void TocarSom()
    {
        if (_urgente) AudioService.PlayUrgente();
        else          AudioService.PlayAviso();
    }

    // ── Fechar ───────────────────────────────────────────────────────────

    private void BtnOk_Click(object sender, RoutedEventArgs e)
        => Fechar(byUser: true);

    private void Fechar(bool byUser)
    {
        ClosedByUser = byUser;
        _soundTimer?.Stop();
        _clockTimer?.Stop();
        _countdownTimer?.Stop();
        _autoCloseTimer?.Stop();
        AudioService.StopAll();
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _soundTimer?.Stop();
        _clockTimer?.Stop();
        _countdownTimer?.Stop();
        _autoCloseTimer?.Stop();
        AudioService.StopAll();
        base.OnClosing(e);
    }
}
