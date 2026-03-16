using System.IO;
using System.Text.Json;
using CopaFormGui.Models;

namespace CopaFormGui.Services;

public class SettingsService : ISettingsService
{
    private static readonly string SettingsFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CopaFormGui");

    private static readonly string MachineSettingsPath = Path.Combine(SettingsFolder, "machine_settings.json");
    private static readonly string ConnectionSettingsPath = Path.Combine(SettingsFolder, "connection_settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public MachineSettings LoadSettings()
    {
        try
        {
            if (File.Exists(MachineSettingsPath))
            {
                var json = File.ReadAllText(MachineSettingsPath);
                return JsonSerializer.Deserialize<MachineSettings>(json) ?? new MachineSettings();
            }
        }
        catch { /* Use defaults on error */ }
        return new MachineSettings();
    }

    public void SaveSettings(MachineSettings settings)
    {
        EnsureFolderExists();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(MachineSettingsPath, json);
    }

    public ConnectionSettings LoadConnectionSettings()
    {
        try
        {
            if (File.Exists(ConnectionSettingsPath))
            {
                var json = File.ReadAllText(ConnectionSettingsPath);
                return JsonSerializer.Deserialize<ConnectionSettings>(json) ?? new ConnectionSettings();
            }
        }
        catch { /* Use defaults on error */ }
        return new ConnectionSettings();
    }

    public void SaveConnectionSettings(ConnectionSettings settings)
    {
        EnsureFolderExists();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(ConnectionSettingsPath, json);
    }

    private static void EnsureFolderExists()
    {
        if (!Directory.Exists(SettingsFolder))
            Directory.CreateDirectory(SettingsFolder);
    }
}
