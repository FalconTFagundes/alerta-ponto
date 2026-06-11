using System.IO;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using MonitorPonto.Models;
using MonitorPonto.Services;
using MonitorPonto.Views;

namespace MonitorPonto;

public partial class App : Application
{
    private TaskbarIcon? _tray;
    private MonitorService? _monitor;
    private AppConfig _config = new();

    private const string AppName = "MonitorPonto";

    private static readonly string LogPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "startup.log");

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n"); }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            Log($"ERRO FATAL: {ex.ExceptionObject}");

        DispatcherUnhandledException += (s, ex) =>
        {
            Log($"ERRO DISPATCHER: {ex.Exception}");
            ex.Handled = true;
        };

        try
        {
            Log("=== Iniciando MonitorPonto ===");
            base.OnStartup(e);

            // Registra na inicialização do Windows automaticamente
            RegistrarInicioAutomatico();

            _config  = ConfigService.Load();
            Log($"Config: {_config.Person.Nome} / id={_config.Person.IdPerson}");

            _monitor = new MonitorService(_config);

            CriarTray();
            Log("Tray criado");

            if (!ConfigService.IsConfigured(_config))
            {
                Log("Config incompleto — abrindo configurações");
                MessageBox.Show(
                    "Bem-vindo ao Monitor de Ponto!\n\nPreencha suas configurações para começar.",
                    "Primeira configuração", MessageBoxButton.OK, MessageBoxImage.Information);
                AbrirConfiguracoes();
            }
            else
            {
                Log("Iniciando monitoramento");
                _monitor.Start();
                _tray!.ShowBalloonTip("Monitor de Ponto", "Monitoramento iniciado ✅", BalloonIcon.Info);
            }

            Log("Startup OK");
        }
        catch (Exception ex)
        {
            Log($"ERRO startup: {ex}");
            MessageBox.Show($"Erro ao iniciar:\n\n{ex.Message}\n\nLog: {LogPath}",
                "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Inicialização automática com o Windows ───────────────────────────

    private static void RegistrarInicioAutomatico()
    {
        try
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return;

            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);

            if (key == null) return;

            var atual = key.GetValue(AppName) as string;
            if (atual != exePath)
            {
                key.SetValue(AppName, exePath);
                Log($"Registro de inicialização automática atualizado: {exePath}");
            }
        }
        catch (Exception ex)
        {
            Log($"Aviso: não foi possível registrar inicialização automática: {ex.Message}");
        }
    }

    public static void RemoverInicioAutomatico()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
            key?.DeleteValue(AppName, throwOnMissingValue: false);
        }
        catch { }
    }

    // ── Tray ─────────────────────────────────────────────────────────────

    private void CriarTray()
    {
        _tray = new TaskbarIcon();
        _tray.ToolTipText = "Monitor de Ponto — BigCard";

        try
        {
            var uri = new Uri("pack://application:,,,/Resources/relogio.ico");
            _tray.IconSource = new System.Windows.Media.Imaging.BitmapImage(uri);
        }
        catch (Exception ex) { Log($"Ícone não carregado: {ex.Message}"); }

        var menu = new ContextMenu();

        menu.Items.Add(new MenuItem
        {
            Header     = "⏰  Monitor de Ponto — BigCard",
            IsEnabled  = false,
            FontWeight = System.Windows.FontWeights.Bold
        });
        menu.Items.Add(new Separator());

        var itemRegistros = new MenuItem { Header = "📋  Registros de Hoje" };
        itemRegistros.Click += (_, _) => AbrirRegistros();
        menu.Items.Add(itemRegistros);

        var itemConfig = new MenuItem { Header = "⚙️  Configurações" };
        itemConfig.Click += (_, _) => AbrirConfiguracoes();
        menu.Items.Add(itemConfig);

        menu.Items.Add(new Separator());

        var itemSair = new MenuItem { Header = "❌  Sair" };
        itemSair.Click += (_, _) => Sair();
        menu.Items.Add(itemSair);

        _tray.ContextMenu = menu;
        _tray.TrayMouseDoubleClick += (_, _) => AbrirRegistros();
    }

    // ── Registros ────────────────────────────────────────────────────────

    private void AbrirRegistros()
    {
        var win = new PunchStatusWindow(_config);
        win.Show();
        win.Activate();
    }

    // ── Configurações ────────────────────────────────────────────────────

    private void AbrirConfiguracoes()
    {
        var win = new ConfigWindow(_config);
        win.ShowDialog();

        if (win.Saved)
        {
            _config = ConfigService.Load();
            _monitor?.StopAllCycles();
            _monitor?.Stop();
            _monitor = new MonitorService(_config);
            _monitor.Start();
            Log("Config atualizado — monitoramento reiniciado");
            _tray?.ShowBalloonTip("Monitor de Ponto",
                "Configurações salvas. Monitoramento reiniciado.", BalloonIcon.Info);
        }
    }

    // ── Encerrar ─────────────────────────────────────────────────────────

    private void Sair()
    {
        Log("Encerrando");
        AudioService.StopAll();
        _monitor?.StopAllCycles();
        _monitor?.Stop();
        _tray?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log("OnExit");
        AudioService.StopAll();
        _monitor?.StopAllCycles();
        _monitor?.Stop();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
