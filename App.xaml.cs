using System.IO;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using MonitorPonto.Models;
using MonitorPonto.Services;
using MonitorPonto.Views;

namespace MonitorPonto;

public partial class App : Application
{
    private TaskbarIcon? _tray;
    private MonitorService? _monitor;
    private AppConfig _config = new();

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

            _config  = ConfigService.Load();
            Log($"Config: {_config.Person.Nome} / id={_config.Person.IdPerson}");

            _monitor = new MonitorService(_config);
            _monitor.AlertRequested += OnAlertRequested;

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

        var itemConfig = new MenuItem { Header = "⚙️  Configurações" };
        itemConfig.Click += (_, _) => AbrirConfiguracoes();
        menu.Items.Add(itemConfig);

        menu.Items.Add(new Separator());

        var itemSair = new MenuItem { Header = "❌  Sair" };
        itemSair.Click += (_, _) => Sair();
        menu.Items.Add(itemSair);

        _tray.ContextMenu = menu;
        _tray.TrayMouseDoubleClick += (_, _) => AbrirConfiguracoes();
    }

    private void AbrirConfiguracoes()
    {
        var win = new ConfigWindow(_config);
        win.ShowDialog();

        if (win.Saved)
        {
            _config = ConfigService.Load();
            _monitor?.Stop();
            _monitor = new MonitorService(_config);
            _monitor.AlertRequested += OnAlertRequested;
            _monitor.Start();
            Log("Config atualizado — monitoramento reiniciado");
            _tray?.ShowBalloonTip("Monitor de Ponto",
                "Configurações salvas. Monitoramento reiniciado.", BalloonIcon.Info);
        }
    }

    private void OnAlertRequested(AlertTipo tipo, AlertNivel nivel,
        string nome, string info1, string info2)
    {
        Dispatcher.Invoke(() =>
        {
            var win = new AlertWindow(tipo, nivel, nome, info1, info2);
            win.Show();
            win.Activate();
        });
    }

    private void Sair()
    {
        Log("Encerrando por solicitação do usuário");
        AudioService.StopAll();
        _monitor?.Stop();
        _tray?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log("OnExit");
        AudioService.StopAll();
        _monitor?.Stop();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
