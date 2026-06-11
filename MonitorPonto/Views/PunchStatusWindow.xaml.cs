using System.Windows;
using System.Windows.Media;
using MonitorPonto.Models;
using MonitorPonto.Services;

namespace MonitorPonto.Views;

public partial class PunchStatusWindow : Window
{
    private readonly RhidService _rhid;
    private readonly AppConfig _config;

    public PunchStatusWindow(AppConfig config)
    {
        InitializeComponent();
        _config = config;
        _rhid   = new RhidService(config);

        TxtData.Text  = $"Data: {DateTime.Now:dd/MM/yyyy}";
        TxtNome.Text  = $"👤  {config.Person.Nome}";

        Loaded += async (_, _) => await CarregarAsync();
    }

    // ── Carregar batidas ─────────────────────────────────────────────────

    private async Task CarregarAsync()
    {
        TxtLoading.Visibility  = Visibility.Visible;
        TxtVazio.Visibility    = Visibility.Collapsed;
        ListaBatidas.Visibility = Visibility.Collapsed;
        BtnAtualizar.IsEnabled = false;

        try
        {
            var records = await _rhid.GetPunchRecordsTodayAsync();
            var idPerson = _config.Person.IdPerson.ToString();
            var record   = records.FirstOrDefault(r => r.EmployeeId == idPerson);

            TxtLoading.Visibility = Visibility.Collapsed;

            if (record == null || record.Punches.Count == 0)
            {
                TxtVazio.Visibility = Visibility.Visible;
            }
            else
            {
                ListaBatidas.Visibility = Visibility.Visible;
                ListaBatidas.ItemsSource = BuildItems(record.Punches);
            }

            TxtUltimaAtualizacao.Text = $"Atualizado às {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            TxtLoading.Visibility = Visibility.Collapsed;
            TxtVazio.Text         = $"Erro ao consultar: {ex.Message}";
            TxtVazio.Visibility   = Visibility.Visible;
        }
        finally
        {
            BtnAtualizar.IsEnabled = true;
        }
    }

    // ── Montar lista de itens ────────────────────────────────────────────

    private static List<BatidaItem> BuildItems(List<Punch> punches)
    {
        var items    = new List<BatidaItem>();
        int entradas = 0, saidas = 0;
        string? tipoAnterior = null;

        foreach (var p in punches)
        {
            bool isEntrada  = p.Type == "ENTRADA";
            bool duplicada  = p.Type == tipoAnterior;
            tipoAnterior    = p.Type;

            if (isEntrada) entradas++;
            else           saidas++;

            string seq;
            if (duplicada)
            {
                seq = isEntrada
                    ? "⚠️ Entrada duplicada (ignorada pelo sistema)"
                    : "⚠️ Saída duplicada (ignorada pelo sistema)";
            }
            else
            {
                seq = isEntrada
                    ? entradas == 1 ? "Entrada no trabalho"
                    : "Retorno do almoço"
                    : saidas == 1 ? "Saída para o almoço"
                    : "Saída do trabalho";
            }

            items.Add(new BatidaItem
            {
                Icone     = duplicada ? "⚠️" : isEntrada ? "🟢" : "🔴",
                Tipo      = isEntrada ? "Entrada" : "Saída",
                Sequencia = seq,
                Hora      = p.Time,
                Cor       = duplicada ? "#AAAAAA" : isEntrada ? "#2d9e4f" : "#e94560"
            });
        }

        return items;
    }

    // ── Eventos ──────────────────────────────────────────────────────────

    private async void BtnAtualizar_Click(object sender, RoutedEventArgs e)
        => await CarregarAsync();

    private void BtnFechar_Click(object sender, RoutedEventArgs e)
        => Close();
}

public class BatidaItem
{
    public string Icone     { get; set; } = "";
    public string Tipo      { get; set; } = "";
    public string Sequencia { get; set; } = "";
    public string Hora      { get; set; } = "";
    public string Cor       { get; set; } = "#2d2d2d";
}
