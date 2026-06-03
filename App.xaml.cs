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
        try
        {
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n");
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Captura qualquer exceção não tratada
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
            Log("OnStartup OK");

            _config = ConfigService.Load();
            Log($"Config carregado. Person: {_config.Person.Nome} / {_config.Person.IdPerson}");

            _monitor = new MonitorService(_config);
            _monitor.AlertRequested += OnAlertRequested;
            Log("MonitorService criado");

            CriarTray();
	    MonitorPonto.Services.AudioService.PlayAlert();
            Log("Tray criado");
	    TesteAudio.ListarDispositivos();

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
                Log("Config OK — iniciando monitoramento");
                _monitor.Start();
                _tray!.ShowBalloonTip("Monitor de Ponto", "Monitoramento iniciado ✅", BalloonIcon.Info);
            }

            Log("Startup concluído com sucesso");
        }
        catch (Exception ex)
        {
            Log($"ERRO no startup: {ex}");
            MessageBox.Show($"Erro ao iniciar:\n\n{ex.Message}\n\nDetalhes em: {LogPath}",
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
        catch (Exception ex)
        {
            Log($"Aviso: ícone não carregado — {ex.Message}");
        }

        var menu = new ContextMenu();

        var itemStatus = new MenuItem
        {
            Header = "⏰  Monitor de Ponto — BigCard",
            IsEnabled = false,
            FontWeight = FontWeights.Bold
        };

        var itemConfig = new MenuItem { Header = "⚙️  Configurações" };
        itemConfig.Click += (_, _) => AbrirConfiguracoes();

        var itemSair = new MenuItem { Header = "❌  Sair" };
        itemSair.Click += (_, _) => Sair();

        menu.Items.Add(itemStatus);
        menu.Items.Add(new Separator());
        menu.Items.Add(itemConfig);
        menu.Items.Add(new Separator());
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
        _monitor?.Stop();
        _tray?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log("Encerrando...");
        _monitor?.Stop();
        _tray?.Dispose();
        base.OnExit(e);
    }
}