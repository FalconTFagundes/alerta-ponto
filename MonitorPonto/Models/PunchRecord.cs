namespace MonitorPonto.Models;

public class PunchRecord
{
    public string EmployeeId   { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    public string Date         { get; set; } = "";
    public List<Punch> Punches { get; set; } = new();
}

public class Punch
{
    public string Time { get; set; } = "";  // "HH:mm"
    public string Type { get; set; } = "";  // "ENTRADA" | "SAIDA"
}

public enum AlertTipo  { Entrada, Almoco, Saida }
public enum AlertNivel { Aviso, Urgente }
