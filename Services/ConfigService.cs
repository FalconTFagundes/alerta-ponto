using System.IO;
using System.Text.Json;
using MonitorPonto.Models;

namespace MonitorPonto.Services;

public static class ConfigService
{
    private static readonly string ConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
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
                var defaultCfg = new AppConfig();
                Save(defaultCfg);
                return defaultCfg;
            }

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOpts) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public static void Save(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOpts);
        File.WriteAllText(ConfigPath, json);
    }

    public static bool IsConfigured(AppConfig config)
    {
        return !string.IsNullOrWhiteSpace(config.Rhid.Username)
            && !string.IsNullOrWhiteSpace(config.Rhid.Password)
            && !string.IsNullOrWhiteSpace(config.Rhid.Domain)
            && config.Person.IdPerson > 0
            && !string.IsNullOrWhiteSpace(config.Person.Nome);
    }
}
