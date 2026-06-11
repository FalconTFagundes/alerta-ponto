using System.Text.Json.Serialization;

namespace MonitorPonto.Models;

public class AppConfig
{
    [JsonPropertyName("rhid")]
    public RhidConfig Rhid { get; set; } = new();

    [JsonPropertyName("person")]
    public PersonConfig Person { get; set; } = new();

    [JsonPropertyName("schedule")]
    public ScheduleConfig Schedule { get; set; } = new();

    [JsonPropertyName("monitor")]
    public MonitorConfig Monitor { get; set; } = new();
}

public class RhidConfig
{
    // ── Fixos — não expostos ao usuário ──────────────────────────────────
    [JsonIgnore] public string BaseUrl   { get; set; } = "https://www.rhid.com.br/v2/api.svc";
    [JsonIgnore] public string ReppUrl   { get; set; } = "https://repp.rhid.com.br";
    [JsonIgnore] public string Domain    { get; set; } = "bigcard";
    [JsonIgnore] public string CompanyId { get; set; } = "55477";

    // ── Configuráveis pelo usuário ────────────────────────────────────────
    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";

    [JsonPropertyName("token_refresh_interval_minutes")]
    public int TokenRefreshMinutes { get; set; } = 50;
}

public class PersonConfig
{
    [JsonPropertyName("id_person")]
    public int IdPerson { get; set; } = 0;

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = "";
}

public class ScheduleConfig
{
    [JsonPropertyName("horario_ativo")]
    public HorarioAtivo HorarioAtivo { get; set; } = new();

    [JsonPropertyName("entrada")]
    public EventoConfig Entrada { get; set; } = new()
    {
        Enabled = true, Horario = "08:00",
        AntecedenciaMinutos = 1, ToleranciaMinutos = 3
    };

    [JsonPropertyName("almoco")]
    public AlmocoConfig Almoco { get; set; } = new();

    [JsonPropertyName("saida")]
    public EventoConfig Saida { get; set; } = new()
    {
        Enabled = true, Horario = "18:00",
        AntecedenciaMinutos = 1, ToleranciaMinutos = 3
    };
}

public class HorarioAtivo
{
    [JsonPropertyName("inicio")] public string Inicio { get; set; } = "07:00";
    [JsonPropertyName("fim")]    public string Fim    { get; set; } = "19:00";
}

public class EventoConfig
{
    [JsonPropertyName("enabled")]              public bool   Enabled             { get; set; } = true;
    [JsonPropertyName("horario")]              public string Horario             { get; set; } = "08:00";
    [JsonPropertyName("antecedencia_minutos")] public int    AntecedenciaMinutos { get; set; } = 1;
    [JsonPropertyName("tolerancia_minutos")]   public int    ToleranciaMinutos   { get; set; } = 3;
}

public class AlmocoConfig
{
    [JsonPropertyName("enabled")]              public bool   Enabled             { get; set; } = true;
    [JsonPropertyName("janela_inicio")]        public string JanelaInicio        { get; set; } = "11:00";
    [JsonPropertyName("janela_fim")]           public string JanelaFim           { get; set; } = "14:00";
    [JsonPropertyName("duracao_minutos")]      public int    DuracaoMinutos      { get; set; } = 90;
    [JsonPropertyName("antecedencia_minutos")] public int    AntecedenciaMinutos { get; set; } = 1;
    [JsonPropertyName("tolerancia_minutos")]   public int    ToleranciaMinutos   { get; set; } = 3;
}

public class MonitorConfig
{
    [JsonPropertyName("polling_interval_seconds")]  public int PollingIntervalSeconds { get; set; } = 60;
    [JsonPropertyName("polling_proximo_seconds")]   public int PollingProximoSeconds  { get; set; } = 30;
    [JsonPropertyName("janela_proxima_minutos")]    public int JanelaProximaMinutos   { get; set; } = 5;
}
