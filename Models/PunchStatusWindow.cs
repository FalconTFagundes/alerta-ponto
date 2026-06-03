using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MonitorPonto.Models;
using MonitorPonto.Services;

namespace MonitorPonto.Views;

/// <summary>
/// Janela de diagnóstico: exibe as batidas do dia em tempo real,
/// confirmando que a API está respondendo e os registros chegando certo.
/// </summary>
public class PunchStatusWindow : Window
{
    private readonly MonitorService _monitor;
    private readonly AppConfig _config;

    // Controles
    private TextBlock _lblNome = null!;
    private TextBlock _lblUltimaAtualizacao = null!;
    private TextBlock _lblStatus = null!;
    private StackPanel _painelBatidas = null!;
    private TextBlock _lblDiagnostico = null!;
    private Button _btnAtualizar = null!;
    private TextBlock _lblProximoEvento = null!;

    private List<PunchRecord> _ultimosRecords = new();

    public PunchStatusWindow(MonitorService monitor, AppConfig config)
    {
        _monitor = monitor;
        _config = config;

        Title = "Batidas de Hoje — Monitor de Ponto";
        Width = 480;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.CanMinimize;
        Background = new SolidColorBrush(Color.FromRgb(18, 18, 24));

        BuildUI();

        // Escuta atualizações do monitor em background
        _monitor.RecordsUpdated += OnRecordsUpdated;

        Closed += (_, _) => _monitor.RecordsUpdated -= OnRecordsUpdated;

        // Atualiza o relógio "próximo evento" a cada segundo
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => AtualizarProximoEvento();
        timer.Start();

        // Busca imediata ao abrir — não espera o próximo ciclo do monitor
        Loaded += async (_, _) => await AtualizarManualAsync();
    }

    // ── Construção da UI ─────────────────────────────────────────────────

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // header
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // conteúdo
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // próximo evento
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // rodapé

        // ─── Header ───────────────────────────────────────────────────────
        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 42)),
            Padding = new Thickness(20, 14, 20, 14)
        };
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _lblNome = new TextBlock
        {
            Text = $"👤  {_config.Person.Nome}",
            Foreground = Brushes.White,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold
        };

        _lblUltimaAtualizacao = new TextBlock
        {
            Text = "Aguardando...",
            Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 160)),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };

        Grid.SetColumn(_lblNome, 0);
        Grid.SetColumn(_lblUltimaAtualizacao, 1);
        headerGrid.Children.Add(_lblNome);
        headerGrid.Children.Add(_lblUltimaAtualizacao);
        header.Child = headerGrid;
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        // ─── Conteúdo principal ───────────────────────────────────────────
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var conteudo = new StackPanel { Margin = new Thickness(20, 16, 20, 8) };

        // Status
        _lblStatus = new TextBlock
        {
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 16),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 220))
        };
        conteudo.Children.Add(_lblStatus);

        // Título batidas
        conteudo.Children.Add(new TextBlock
        {
            Text = "🕐  Batidas registradas hoje",
            Foreground = new SolidColorBrush(Color.FromRgb(120, 180, 255)),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Painel das batidas
        _painelBatidas = new StackPanel();
        conteudo.Children.Add(_painelBatidas);

        // Diagnóstico da API
        var sepDiag = new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(50, 50, 70)),
            Margin = new Thickness(0, 16, 0, 12)
        };
        conteudo.Children.Add(sepDiag);

        conteudo.Children.Add(new TextBlock
        {
            Text = "🔧  Diagnóstico da API",
            Foreground = new SolidColorBrush(Color.FromRgb(120, 180, 255)),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        _lblDiagnostico = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 180)),
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas, Courier New")
        };
        conteudo.Children.Add(_lblDiagnostico);

        scroll.Content = conteudo;
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        // ─── Próximo evento ───────────────────────────────────────────────
        var borderProximo = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(25, 35, 55)),
            Padding = new Thickness(20, 10, 20, 10)
        };
        _lblProximoEvento = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 220, 255)),
            TextWrapping = TextWrapping.Wrap
        };
        borderProximo.Child = _lblProximoEvento;
        Grid.SetRow(borderProximo, 2);
        root.Children.Add(borderProximo);

        // ─── Rodapé ───────────────────────────────────────────────────────
        var footer = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 42)),
            Padding = new Thickness(20, 10, 20, 10)
        };
        var footerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        _btnAtualizar = new Button
        {
            Content = "🔄  Atualizar agora",
            Padding = new Thickness(16, 6, 16, 6),
            Background = new SolidColorBrush(Color.FromRgb(50, 100, 200)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            FontSize = 12
        };
        _btnAtualizar.Click += async (_, _) => await AtualizarManualAsync();

        footerPanel.Children.Add(_btnAtualizar);
        footer.Child = footerPanel;
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        Content = root;

        // Estado inicial
        AtualizarUI(new List<PunchRecord>(), primeiraVez: true);
        AtualizarProximoEvento();
    }

    // ── Atualização via evento do MonitorService ─────────────────────────

    private void OnRecordsUpdated(List<PunchRecord> records)
    {
        Dispatcher.Invoke(() =>
        {
            _ultimosRecords = records;
            AtualizarUI(records);
        });
    }

    // ── Atualização manual ────────────────────────────────────────────────

    private async Task AtualizarManualAsync()
    {
        _btnAtualizar.IsEnabled = false;
        _btnAtualizar.Content = "⏳  Buscando...";
        _lblStatus.Text = "Consultando API...";
        _lblStatus.Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 100));

        try
        {
            // Cria um RhidService temporário para consulta direta
            var rhid = new RhidService(_config);
            var records = await rhid.GetPunchRecordsTodayAsync();
            _ultimosRecords = records;
            AtualizarUI(records);
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Erro na consulta: {ex.Message}";
            _lblStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
        }
        finally
        {
            _btnAtualizar.IsEnabled = true;
            _btnAtualizar.Content = "🔄  Atualizar agora";
        }
    }

    // ── Renderização principal ────────────────────────────────────────────

    private void AtualizarUI(List<PunchRecord> records, bool primeiraVez = false)
    {
        _lblUltimaAtualizacao.Text = primeiraVez
            ? "Aguardando 1º ciclo..."
            : $"Atualizado: {DateTime.Now:HH:mm:ss}";

        var idPerson = _config.Person.IdPerson.ToString();
        var record = records.FirstOrDefault(r => r.EmployeeId == idPerson);

        _painelBatidas.Children.Clear();

        if (primeiraVez)
        {
            _lblStatus.Text = "O monitor buscará os registros automaticamente no próximo ciclo.";
            _lblStatus.Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 180));
            _lblDiagnostico.Text = $"ID monitorado: {_config.Person.IdPerson}\nURL base: {_config.Rhid.ReppUrl}";

            _painelBatidas.Children.Add(new TextBlock
            {
                Text = "—  Nenhum dado ainda",
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 120)),
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 4)
            });
            return;
        }

        // ─ Status geral ────────────────────────────────────────────────
        if (records.Count == 0)
        {
            _lblStatus.Text = "⚠️  API retornou vazio. Verifique as credenciais ou se há ponto registrado hoje.";
            _lblStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 180, 60));
        }
        else if (record == null)
        {
            _lblStatus.Text = $"⚠️  API respondeu ({records.Count} funcionário(s)), mas ID {idPerson} não foi encontrado.";
            _lblStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 180, 60));
        }
        else
        {
            var n = record.Punches.Count;
            _lblStatus.Text = n == 0
                ? "Nenhuma batida registrada hoje ainda."
                : $"✅  {n} batida{(n > 1 ? "s" : "")} encontrada{(n > 1 ? "s" : "")} para {record.EmployeeName}.";
            _lblStatus.Foreground = n == 0
                ? new SolidColorBrush(Color.FromRgb(200, 200, 100))
                : new SolidColorBrush(Color.FromRgb(100, 220, 130));
        }

        // ─ Lista de batidas ────────────────────────────────────────────
        if (record != null && record.Punches.Count > 0)
        {
            for (int i = 0; i < record.Punches.Count; i++)
            {
                var p = record.Punches[i];
                var numero = i + 1;
                var label = ObterLabelBatida(numero, record.Punches.Count);
                var cor = p.Type == "ENTRADA"
                    ? Color.FromRgb(100, 220, 130)
                    : Color.FromRgb(255, 140, 80);

                var linha = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 32, 48)),
                    CornerRadius = new CornerRadius(6),
                    Margin = new Thickness(0, 0, 0, 6),
                    Padding = new Thickness(14, 8, 14, 8)
                };

                var linhaGrid = new Grid();
                linhaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                linhaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                linhaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var lblNumero = new TextBlock
                {
                    Text = $"{numero}.",
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 130)),
                    FontSize = 13,
                    Width = 24
                };

                var lblLabel = new TextBlock
                {
                    Text = label,
                    Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 220)),
                    FontSize = 13
                };

                var lblHorario = new TextBlock
                {
                    Text = p.Time,
                    Foreground = new SolidColorBrush(cor),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold
                };

                Grid.SetColumn(lblNumero, 0);
                Grid.SetColumn(lblLabel, 1);
                Grid.SetColumn(lblHorario, 2);
                linhaGrid.Children.Add(lblNumero);
                linhaGrid.Children.Add(lblLabel);
                linhaGrid.Children.Add(lblHorario);
                linha.Child = linhaGrid;
                _painelBatidas.Children.Add(linha);
            }
        }
        else if (!primeiraVez)
        {
            _painelBatidas.Children.Add(new TextBlock
            {
                Text = "—  Nenhuma batida hoje ainda",
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 120)),
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        // ─ Diagnóstico ────────────────────────────────────────────────
        var diag = new System.Text.StringBuilder();
        diag.AppendLine($"ID monitorado : {_config.Person.IdPerson}");
        diag.AppendLine($"URL REPP      : {_config.Rhid.ReppUrl}");
        diag.AppendLine($"Total IDs na resposta : {records.Count}");

        if (records.Count > 0)
        {
            diag.AppendLine("IDs retornados:");
            foreach (var r in records.Take(5))
                diag.AppendLine($"  • {r.EmployeeId}  ({r.EmployeeName})  — {r.Punches.Count} batida(s)");
            if (records.Count > 5)
                diag.AppendLine($"  ...e mais {records.Count - 5}");
        }

        _lblDiagnostico.Text = diag.ToString().TrimEnd();
    }

    // ── Próximo evento ────────────────────────────────────────────────────

    private void AtualizarProximoEvento()
    {
        var now = DateTime.Now;
        var hoje = now.Date;

        var eventos = new List<(DateTime Quando, string Descricao)>();

        void Add(string horario, string desc)
        {
            if (TimeOnly.TryParse(horario, out var t))
                eventos.Add((hoje.Add(t.ToTimeSpan()), desc));
        }

        var e = _config.Schedule.Entrada;
        if (e.Enabled)
        {
            Add(e.Horario, "🟢 Entrada");
        }

        var s = _config.Schedule.Saida;
        if (s.Enabled)
        {
            Add(s.Horario, "🔴 Saída");
        }

        var a = _config.Schedule.Almoco;
        if (a.Enabled)
        {
            if (TimeOnly.TryParse(a.JanelaInicio, out var ji))
                eventos.Add((hoje.Add(ji.ToTimeSpan()), "🟡 Almoço (início janela)"));
            if (TimeOnly.TryParse(a.JanelaFim, out var jf))
                eventos.Add((hoje.Add(jf.ToTimeSpan()), "🟡 Almoço (fim janela)"));
        }

        var proximos = eventos
            .Where(ev => ev.Quando > now)
            .OrderBy(ev => ev.Quando)
            .ToList();

        if (proximos.Count == 0)
        {
            _lblProximoEvento.Text = "⏰  Sem mais eventos programados para hoje.";
            return;
        }

        var proximo = proximos.First();
        var diff = proximo.Quando - now;
        var diffStr = diff.TotalHours >= 1
            ? $"{(int)diff.TotalHours}h {diff.Minutes:D2}min"
            : diff.TotalMinutes >= 1
                ? $"{(int)diff.TotalMinutes}min {diff.Seconds:D2}s"
                : $"{diff.Seconds}s";

        _lblProximoEvento.Text = $"⏰  Próximo evento: {proximo.Descricao}  às {proximo.Quando:HH:mm}  (em {diffStr})";
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string ObterLabelBatida(int numero, int total)
    {
        return numero switch
        {
            1 => "Entrada",
            2 => "Saída almoço",
            3 => "Retorno almoço",
            4 => "Saída",
            _ => $"Batida {numero}"
        };
    }
}