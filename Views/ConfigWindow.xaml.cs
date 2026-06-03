using System.Windows;
using MonitorPonto.Models;
using MonitorPonto.Services;

namespace MonitorPonto.Views;

public partial class ConfigWindow : Window
{
    private AppConfig _config;
    public bool Saved { get; private set; } = false;

    public ConfigWindow(AppConfig config)
    {
        InitializeComponent();
        _config = config;
        CarregarCampos();
    }

    // ── Carregar valores do config nos campos ───────────────────────────

    private void CarregarCampos()
    {
        // Conta RHiD
        TxtUsername.Text = _config.Rhid.Username;
        TxtPassword.Password = _config.Rhid.Password;
        TxtDomain.Text = _config.Rhid.Domain;

        // Pessoa
        TxtNome.Text = _config.Person.Nome;
        TxtIdPerson.Text = _config.Person.IdPerson > 0 ? _config.Person.IdPerson.ToString() : "";

        // Entrada
        ChkEntradaEnabled.IsChecked = _config.Schedule.Entrada.Enabled;
        TxtEntradaHorario.Text = _config.Schedule.Entrada.Horario;
        TxtEntradaAntec.Text = _config.Schedule.Entrada.AntecedenciaMinutos.ToString();
        TxtEntradaToler.Text = _config.Schedule.Entrada.ToleranciaMinutos.ToString();

        // Almoço
        ChkAlmocoEnabled.IsChecked = _config.Schedule.Almoco.Enabled;
        TxtAlmocoJanelaIni.Text = _config.Schedule.Almoco.JanelaInicio;
        TxtAlmocoJanelaFim.Text = _config.Schedule.Almoco.JanelaFim;
        TxtAlmocoDuracao.Text = _config.Schedule.Almoco.DuracaoMinutos.ToString();
        TxtAlmocoAntec.Text = _config.Schedule.Almoco.AntecedenciaMinutos.ToString();
        TxtAlmocoToler.Text = _config.Schedule.Almoco.ToleranciaMinutos.ToString();

        // Saída
        ChkSaidaEnabled.IsChecked = _config.Schedule.Saida.Enabled;
        TxtSaidaHorario.Text = _config.Schedule.Saida.Horario;
        TxtSaidaAntec.Text = _config.Schedule.Saida.AntecedenciaMinutos.ToString();
        TxtSaidaToler.Text = _config.Schedule.Saida.ToleranciaMinutos.ToString();

        // Avançado
        TxtHorarioIni.Text = _config.Schedule.HorarioAtivo.Inicio;
        TxtHorarioFim.Text = _config.Schedule.HorarioAtivo.Fim;
        TxtPollingNormal.Text = _config.Monitor.PollingIntervalSeconds.ToString();
        TxtPollingProximo.Text = _config.Monitor.PollingProximoSeconds.ToString();
        TxtJanela.Text = _config.Monitor.JanelaProximaMinutos.ToString();
    }

    // ── Salvar ──────────────────────────────────────────────────────────

    private void BtnSalvar_Click(object sender, RoutedEventArgs e)
    {
        if (!Validar()) return;

        _config.Rhid.Username = TxtUsername.Text.Trim();
        _config.Rhid.Password = TxtPassword.Password;
        _config.Rhid.Domain = TxtDomain.Text.Trim();
        _config.Rhid.CompanyId = "55477";

        _config.Person.Nome = TxtNome.Text.Trim();
        _config.Person.IdPerson = int.Parse(TxtIdPerson.Text.Trim());

        _config.Schedule.Entrada.Enabled = ChkEntradaEnabled.IsChecked == true;
        _config.Schedule.Entrada.Horario = TxtEntradaHorario.Text.Trim();
        _config.Schedule.Entrada.AntecedenciaMinutos = int.Parse(TxtEntradaAntec.Text.Trim());
        _config.Schedule.Entrada.ToleranciaMinutos = int.Parse(TxtEntradaToler.Text.Trim());

        _config.Schedule.Almoco.Enabled = ChkAlmocoEnabled.IsChecked == true;
        _config.Schedule.Almoco.JanelaInicio = TxtAlmocoJanelaIni.Text.Trim();
        _config.Schedule.Almoco.JanelaFim = TxtAlmocoJanelaFim.Text.Trim();
        _config.Schedule.Almoco.DuracaoMinutos = int.Parse(TxtAlmocoDuracao.Text.Trim());
        _config.Schedule.Almoco.AntecedenciaMinutos = int.Parse(TxtAlmocoAntec.Text.Trim());
        _config.Schedule.Almoco.ToleranciaMinutos = int.Parse(TxtAlmocoToler.Text.Trim());

        _config.Schedule.Saida.Enabled = ChkSaidaEnabled.IsChecked == true;
        _config.Schedule.Saida.Horario = TxtSaidaHorario.Text.Trim();
        _config.Schedule.Saida.AntecedenciaMinutos = int.Parse(TxtSaidaAntec.Text.Trim());
        _config.Schedule.Saida.ToleranciaMinutos = int.Parse(TxtSaidaToler.Text.Trim());

        _config.Schedule.HorarioAtivo.Inicio = TxtHorarioIni.Text.Trim();
        _config.Schedule.HorarioAtivo.Fim = TxtHorarioFim.Text.Trim();

        _config.Monitor.PollingIntervalSeconds = int.Parse(TxtPollingNormal.Text.Trim());
        _config.Monitor.PollingProximoSeconds = int.Parse(TxtPollingProximo.Text.Trim());
        _config.Monitor.JanelaProximaMinutos = int.Parse(TxtJanela.Text.Trim());

        ConfigService.Save(_config);
        Saved = true;
        TxtStatus.Text = "✅ Configurações salvas!";
        Close();
    }

    // ── Validação ────────────────────────────────────────────────────────

    private bool Validar()
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(TxtUsername.Text)) erros.Add("E-mail é obrigatório");
        if (string.IsNullOrWhiteSpace(TxtPassword.Password)) erros.Add("Senha é obrigatória");
        if (string.IsNullOrWhiteSpace(TxtDomain.Text)) erros.Add("Domínio é obrigatório");
        if (string.IsNullOrWhiteSpace(TxtNome.Text)) erros.Add("Nome é obrigatório");

        if (!int.TryParse(TxtIdPerson.Text, out var id) || id <= 0)
            erros.Add("ID Person deve ser um número maior que zero");

        if (erros.Any())
        {
            MessageBox.Show(string.Join("\n", erros.Select(e => $"• {e}")),
                "Campos obrigatórios", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    // ── Cancelar ─────────────────────────────────────────────────────────

    private void BtnCancelar_Click(object sender, RoutedEventArgs e) => Close();

    // ── Testes de alerta ─────────────────────────────────────────────────

    private void TesteEntradaAviso_Click(object sender, RoutedEventArgs e)
        => new AlertWindow(AlertTipo.Entrada, AlertNivel.Aviso,
            TxtNome.Text.Trim().NullIfEmpty() ?? "Seu Nome", "08:00", "").Show();

    private void TesteEntradaUrgente_Click(object sender, RoutedEventArgs e)
        => new AlertWindow(AlertTipo.Entrada, AlertNivel.Urgente,
            TxtNome.Text.Trim().NullIfEmpty() ?? "Seu Nome", "08:00", "").Show();

    private void TesteAlmocoAviso_Click(object sender, RoutedEventArgs e)
        => new AlertWindow(AlertTipo.Almoco, AlertNivel.Aviso,
            TxtNome.Text.Trim().NullIfEmpty() ?? "Seu Nome", "12:00", "13:30").Show();

    private void TesteAlmocoUrgente_Click(object sender, RoutedEventArgs e)
        => new AlertWindow(AlertTipo.Almoco, AlertNivel.Urgente,
            TxtNome.Text.Trim().NullIfEmpty() ?? "Seu Nome", "12:00", "13:30").Show();

    private void TesteSaidaAviso_Click(object sender, RoutedEventArgs e)
        => new AlertWindow(AlertTipo.Saida, AlertNivel.Aviso,
            TxtNome.Text.Trim().NullIfEmpty() ?? "Seu Nome", "18:00", "").Show();

    private void TesteSaidaUrgente_Click(object sender, RoutedEventArgs e)
        => new AlertWindow(AlertTipo.Saida, AlertNivel.Urgente,
            TxtNome.Text.Trim().NullIfEmpty() ?? "Seu Nome", "18:00", "").Show();
}

// Extension helper
public static class StringExtensions
{
    public static string? NullIfEmpty(this string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s;
}