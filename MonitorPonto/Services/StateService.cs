using System.IO;
using System.Text.Json;
using MonitorPonto.Models;

namespace MonitorPonto.Services;

public class StateService
{
    private readonly string _statePath;
    private readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public StateService()
    {
        _statePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "state.json");
    }

    private Dictionary<string, object> ReadAll()
    {
        try
        {
            if (!File.Exists(_statePath)) return new();
            var json = File.ReadAllText(_statePath);
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
        }
        catch { return new(); }
    }

    private void WriteAll(Dictionary<string, object> data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, _opts);
            File.WriteAllText(_statePath, json);
        }
        catch { }
    }

    public bool Exists(string key)
    {
        return ReadAll().ContainsKey(key);
    }

    public void Set(string key, object value)
    {
        var data = ReadAll();
        data[key] = value;
        WriteAll(data);
    }

    public T? Get<T>(string key)
    {
        var data = ReadAll();
        if (!data.TryGetValue(key, out var val)) return default;
        try
        {
            var json = JsonSerializer.Serialize(val);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch { return default; }
    }

    public void Remove(string key)
    {
        var data = ReadAll();
        data.Remove(key);
        WriteAll(data);
    }

    public void MarkNotified(string key)
    {
        Set(key + "_notified", true);
    }

    public bool IsNotified(string key)
    {
        return Exists(key + "_notified");
    }
}
