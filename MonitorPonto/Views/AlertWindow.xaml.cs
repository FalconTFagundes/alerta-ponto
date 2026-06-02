using System.Media;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MonitorPonto.Models;

namespace MonitorPonto.Views;

public partial class AlertWindow : Window
{
    private DispatcherTimer? _soundTimer;
    private DispatcherTimer? _clockTimer;
    private bool _urgente;

    public AlertWindow(AlertTipo tipo, AlertNivel nivel, string nome, string info1, string info2)
    {
        InitializeComponent();
        _urgente = nivel == AlertNivel.Urgente;
        ConfigurarVisual(tipo, nivel, nome, info1, info2);
        StartSound();
        StartClock();
        Loaded += (_, _) => BtnOk.Focus();
    }

    // ── Visual ──────────────────────────────────────────────────────────

    private void ConfigurarVisual(AlertTipo tipo, AlertNivel nivel, string nome, string info1, string info2)
    {
        string bg, accent, icon, titulo, subtitulo, btnText;

        if (nivel == AlertNivel.Urgente)
        {
            bg       = "#3d0000";
            accent   = "#FF1A1A";
            icon     = "🚨";
            titulo   = "VOCÊ AINDA NÃO BATEU O PONTO!!";
            subtitulo = "O prazo já passou! Vá bater o ponto agora.";
            btnText  = "✅  JÁ FUI BATER O PONTO";
        }
        else
        {
            (bg, accent, icon, titulo, subtitulo, btnText) = tipo switch
            {
                AlertTipo.Entrada => ("#0f3460", "#F5A623", "🌅",
                    "HORA DE BATER A ENTRADA!",
                    "Você ainda não bateu o ponto de entrada.",
                    "✅  JÁ FUI BATER O PONTO"),

                AlertTipo.Almoco => ("#1a1a2e", "#E94560", "⏰",
                    "HORA DE RETORNAR DO ALMOÇO!",
                    "Você não retornou do almoço no horário.",
                    "✅  JÁ FUI BATER O PONTO"),

                AlertTipo.Saida => ("#1b4332", "#52B788", "🌆",
                    "HORA DE BATER A SAÍDA!",
                    "Não esqueça de bater o ponto de saída.",
                    "✅  JÁ FUI BATER O PONTO"),

                _ => ("#1a1a2e", "#E94560", "⏰", "ATENÇÃO!", "", "✅  OK")
            };
        }

        GridRoot.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));

        TxtIcon.Text      = icon;
        TxtTitulo.Text    = titulo;
        TxtTitulo.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accent));
        TxtSubtitulo.Text = subtitulo;
        TxtNome.Text      = $"👤  {nome}";
        BtnOk.Content     = btnText;
        BtnOk.Background  = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accent));

        // Info contextual
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

        // Animação de pulso no botão
        var animation = new ColorAnimation(
            (Color)ColorConverter.ConvertFromString(accent),
            (Color)ColorConverter.ConvertFromString(bg),
            new Duration(TimeSpan.FromMilliseconds(_urgente ? 400 : 700)))
        {
            AutoReverse  = true,
            RepeatBehavior = RepeatBehavior.Forever
        };

        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accent));
        BtnOk.Background = brush;
        brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
    }

    // ── Relógio ─────────────────────────────────────────────────────────

    private void StartClock()
    {
        TxtAgora.Text = $"🕐  Agora: {DateTime.Now:HH:mm}";
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _clockTimer.Tick += (_, _) => TxtAgora.Text = $"🕐  Agora: {DateTime.Now:HH:mm}";
        _clockTimer.Start();
    }

    // ── Som ─────────────────────────────────────────────────────────────

    private void StartSound()
    {
        _soundTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_urgente ? 2 : 3)
        };
        _soundTimer.Tick += (_, _) => PlaySound();
        PlaySound();
        _soundTimer.Start();
    }

    private void PlaySound()
    {
        try
        {
            if (_urgente)
            {
                // 3 beeps críticos rápidos
                SystemSounds.Hand.Play();
                Task.Delay(300).ContinueWith(_ => Dispatcher.Invoke(() => SystemSounds.Hand.Play()));
                Task.Delay(600).ContinueWith(_ => Dispatcher.Invoke(() => SystemSounds.Hand.Play()));
            }
            else
            {
                SystemSounds.Exclamation.Play();
            }
        }
        catch { }
    }

    // ── Fechar ───────────────────────────────────────────────────────────

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        _soundTimer?.Stop();
        _clockTimer?.Stop();
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _soundTimer?.Stop();
        _clockTimer?.Stop();
        base.OnClosing(e);
    }
}
