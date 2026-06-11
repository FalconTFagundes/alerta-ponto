using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MonitorPonto.Services;

public class StateService
{
    private readonly string _path;
    private readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public StateService()
    {
        _path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "state.json");
    }

    private Dictionary<string, JsonNode?> Read()
    {
        try
        {
            if (!File.Exists(_path)) return new();
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<Dictionary<string, JsonNode?>>(json) ?? new();
        }
        catch { return new(); }
    }

    private void Write(Dictionary<string, JsonNode?> data)
    {
        try { File.WriteAllText(_path, JsonSerializer.Serialize(data, _opts)); }
        catch { }
    }

    public bool Exists(string key) => Read().ContainsKey(key);

    public void Set(string key, object value)
    {
        var data = Read();
        data[key] = JsonNode.Parse(JsonSerializer.Serialize(value));
        Write(data);
    }

    public T? Get<T>(string key)
    {
        var data = Read();
        if (!data.TryGetValue(key, out var val) || val == null) return default;
        try { return JsonSerializer.Deserialize<T>(val.ToJsonString()); }
        catch { return default; }
    }

    public void Remove(string key)
    {
        var data = Read();
        data.Remove(key);
        Write(data);
    }
}
