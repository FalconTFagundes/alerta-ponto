using System.IO;
using System.Text.Json;
using MonitorPonto.Models;

namespace MonitorPonto.Services;

public static class ConfigService
{
    private static readonly string ConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "config.json");

    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                var def = new AppConfig();
                Save(def);
                return def;
            }
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, Opts) ?? new AppConfig();
        }
        catch { return new AppConfig(); }
    }

    public static void Save(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, Opts);
        File.WriteAllText(ConfigPath, json);
    }

    public static bool IsConfigured(AppConfig config)
        => !string.IsNullOrWhiteSpace(config.Rhid.Username)
        && !string.IsNullOrWhiteSpace(config.Rhid.Password)
        && config.Person.IdPerson > 0
        && !string.IsNullOrWhiteSpace(config.Person.Nome);
}
