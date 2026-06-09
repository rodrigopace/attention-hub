using System.IO;
using System.Text.Json;

namespace AttentionHub.Client;

public sealed record ClientSettings(
    string BackendUrl,
    int PollIntervalSeconds,
    string DeviceId,
    string DeviceDisplayName)
{
    public static ClientSettings Default => new(
        BackendUrl: "http://localhost:8000",
        PollIntervalSeconds: 120,
        DeviceId: "win-mock-client",
        DeviceDisplayName: Environment.MachineName);
}

public static class ClientSettingsStore
{
    public static string SettingsPath
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "AttentionHub", "client-settings.json");
        }
    }

    public static ClientSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return ClientSettings.Default;
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<ClientSettings>(json, JsonOptions.Wire) ?? ClientSettings.Default;
        }
        catch
        {
            return ClientSettings.Default;
        }
    }

    public static void Save(ClientSettings settings)
    {
        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions.Pretty);
        File.WriteAllText(SettingsPath, json);
    }
}
