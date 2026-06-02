using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using MonitorPonto.Models;

namespace MonitorPonto.Services;

public class RhidService
{
    private readonly HttpClient _http = new();
    private string? _token;
    private DateTime _tokenAcquiredAt = DateTime.MinValue;

    private AppConfig _config;

    public RhidService(AppConfig config)
    {
        _config = config;
    }

    public void UpdateConfig(AppConfig config)
    {
        _config = config;
        _token = null; // força re-autenticação
    }

    // ── Autenticação ────────────────────────────────────────────────────

    private bool TokenValido()
    {
        if (string.IsNullOrEmpty(_token)) return false;
        return (DateTime.Now - _tokenAcquiredAt).TotalMinutes < _config.Rhid.TokenRefreshMinutes;
    }

    private async Task<bool> AutenticarAsync()
    {
        try
        {
            var url = $"{_config.Rhid.BaseUrl}/login";
            var payload = new
            {
                email    = _config.Rhid.Username,
                password = _config.Rhid.Password,
                domain   = _config.Rhid.Domain
            };

            var resp = await _http.PostAsJsonAsync(url, payload);
            if (!resp.IsSuccessStatusCode) return false;

            var json = await resp.Content.ReadAsStringAsync();
            var node = JsonNode.Parse(json);
            var token = node?["accessToken"]?.GetValue<string>();

            if (string.IsNullOrEmpty(token)) return false;

            _token = token;
            _tokenAcquiredAt = DateTime.Now;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> EnsureAuthAsync()
    {
        if (!TokenValido())
            return await AutenticarAsync();
        return true;
    }

    // ── Consulta de ponto ───────────────────────────────────────────────

    public async Task<List<PunchRecord>> GetPunchRecordsTodayAsync()
    {
        var date = DateTime.Now.ToString("yyyy-MM-dd");

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            if (!await EnsureAuthAsync()) return new();

            try
            {
                var url = $"{_config.Rhid.ReppUrl}/ponto_diario?data={date}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", $"Bearer {_token}");
                request.Headers.Add("X-Cid-Rhid", _config.Rhid.CompanyId);
                request.Headers.Add("Origin", "https://www.rhid.com.br");
                request.Headers.Add("Accept", "application/json, text/plain, */*");

                var resp = await _http.SendAsync(request);

                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _token = null;
                    continue;
                }

                if (!resp.IsSuccessStatusCode) continue;

                var json = await resp.Content.ReadAsStringAsync();
                return ParsePontodiario(json, date);
            }
            catch
            {
                await Task.Delay(1000);
            }
        }

        return new();
    }

    private List<PunchRecord> ParsePontodiario(string json, string date)
    {
        try
        {
            var node = JsonNode.Parse(json);
            if (node?["status"]?.GetValue<bool>() != true) return new();

            var retorno = node["retorno"];
            if (retorno == null) return new();

            // Pode ser objeto único ou array
            var items = retorno is JsonArray arr ? arr : new JsonArray { retorno.DeepClone() };

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

                    // dateTimeStr: YYYYMMDDHHII
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
        catch
        {
            return new();
        }
    }
}
