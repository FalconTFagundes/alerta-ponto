using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MonitorPonto.Models;

namespace MonitorPonto.Services;

public class RhidService
{
    // ── Fixos BigCard ────────────────────────────────────────────────────
    private const string BaseUrl   = "https://www.rhid.com.br/v2/api.svc";
    private const string ReppUrl   = "https://repp.rhid.com.br";
    private const string Domain    = "bigcard";
    private const string CompanyId = "55477";

    private readonly HttpClient _http = new();
    private AppConfig _config;
    private string? _token;
    private DateTime _tokenAt = DateTime.MinValue;

    public RhidService(AppConfig config) => _config = config;

    public void UpdateConfig(AppConfig config)
    {
        _config = config;
        _token  = null;
    }

    // ── Autenticação ─────────────────────────────────────────────────────

    private bool TokenValido()
        => !string.IsNullOrEmpty(_token)
        && (DateTime.Now - _tokenAt).TotalMinutes < _config.Rhid.TokenRefreshMinutes;

    private async Task<bool> AutenticarAsync()
    {
        try
        {
            var payload = new { email = _config.Rhid.Username, password = _config.Rhid.Password, domain = Domain };
            var resp = await _http.PostAsJsonAsync($"{BaseUrl}/login", payload);
            if (!resp.IsSuccessStatusCode) return false;
            var json  = await resp.Content.ReadAsStringAsync();
            var node  = JsonNode.Parse(json);
            var token = node?["accessToken"]?.GetValue<string>();
            if (string.IsNullOrEmpty(token)) return false;
            _token = token;
            _tokenAt = DateTime.Now;
            return true;
        }
        catch { return false; }
    }

    private async Task<bool> EnsureAuthAsync()
    {
        if (!TokenValido()) return await AutenticarAsync();
        return true;
    }

    // ── Ponto diário ─────────────────────────────────────────────────────

    public async Task<List<PunchRecord>> GetPunchRecordsTodayAsync()
    {
        var date = DateTime.Now.ToString("yyyy-MM-dd");

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            if (!await EnsureAuthAsync()) return new();

            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get,
                    $"{ReppUrl}/ponto_diario?data={date}");
                req.Headers.Add("Authorization", $"Bearer {_token}");
                req.Headers.Add("X-Cid-Rhid", CompanyId);
                req.Headers.Add("Origin", "https://www.rhid.com.br");
                req.Headers.Add("Accept", "application/json, text/plain, */*");

                var resp = await _http.SendAsync(req);

                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _token = null;
                    continue;
                }

                if (!resp.IsSuccessStatusCode) continue;

                var json = await resp.Content.ReadAsStringAsync();
                return Parse(json, date);
            }
            catch { await Task.Delay(1000); }
        }

        return new();
    }

    private List<PunchRecord> Parse(string json, string date)
    {
        try
        {
            var node = JsonNode.Parse(json);
            if (node?["status"]?.GetValue<bool>() != true) return new();

            var retorno = node["retorno"];
            if (retorno == null) return new();

            var items = retorno is JsonArray arr ? arr
                : new JsonArray { retorno.DeepClone() };

            var results = new List<PunchRecord>();
            foreach (var item in items)
            {
                if (item == null) continue;
                var empId   = item["idPerson"]?.ToString() ?? item["pis"]?.ToString() ?? "?";
                var empName = item["nome"]?.GetValue<string>()?.Trim() ?? $"ID {empId}";
                var afdt    = item["afdt"] as JsonArray ?? new JsonArray();

                var punches = new List<Punch>();
                foreach (var b in afdt)
                {
                    if (b == null) continue;
                    var dtStr = b["dateTimeStr"]?.GetValue<string>() ?? "";
                    var tipo  = b["_typeEntradaSaida"]?.GetValue<string>() ?? "";
                    if (dtStr.Length >= 12)
                    {
                        var hora = $"{dtStr[8..10]}:{dtStr[10..12]}";
                        var type = tipo.ToUpper() is "E" or "ENTRADA" ? "ENTRADA"
                                 : tipo.ToUpper() is "S" or "SAIDA"   ? "SAIDA"
                                 : "UNKNOWN";
                        punches.Add(new Punch { Time = hora, Type = type });
                    }
                }

                results.Add(new PunchRecord
                {
                    EmployeeId   = empId,
                    EmployeeName = empName,
                    Date         = date,
                    Punches      = punches
                });
            }
            return results;
        }
        catch { return new(); }
    }
}
