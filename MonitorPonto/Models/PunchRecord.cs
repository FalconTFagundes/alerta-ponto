namespace MonitorPonto.Models;

public class PunchRecord
{
    public string EmployeeId { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    public string Date { get; set; } = "";
    public List<Punch> Punches { get; set; } = new();
}

public class Punch
{
    public string Time { get; set; } = "";   // "HH:mm"
    public string Type { get; set; } = "";   // "ENTRADA" | "SAIDA"
}

public class AlertState
{
    public string LunchStart { get; set; } = "";
    public string ExpectedReturn { get; set; } = "";
    public string GatilhoAviso { get; set; } = "";
    public string GatilhoUrgente { get; set; } = "";
    public string Nome { get; set; } = "";
}

public enum AlertTipo { Entrada, Almoco, Saida }
public enum AlertNivel { Aviso, Urgente }
