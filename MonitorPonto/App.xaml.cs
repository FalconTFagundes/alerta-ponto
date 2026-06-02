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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _config  = ConfigService.Load();
        _monitor = new MonitorService(_config);
        _monitor.AlertRequested += OnAlertRequested;

        CriarTray();

        if (!ConfigService.IsConfigured(_config))
        {
            MessageBox.Show(
                "Bem-vindo ao Monitor de Ponto!\n\nPreencha suas configurações para começar.",
                "Primeira configuração", MessageBoxButton.OK, MessageBoxImage.Information);
            AbrirConfiguracoes();
        }
        else
        {
            _monitor.Start();
            _tray!.ShowBalloonTip("Monitor de Ponto", "Monitoramento iniciado ✅", BalloonIcon.Info);
        }
    }

    private void CriarTray()
    {
        _tray = new TaskbarIcon();
        _tray.ToolTipText = "Monitor de Ponto — BigCard";

        var menu = new ContextMenu();

        var itemStatus = new MenuItem
        {
            Header     = "⏰  Monitor de Ponto — BigCard",
            IsEnabled  = false,
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
        _monitor?.Stop();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
