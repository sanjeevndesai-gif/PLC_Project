using CopaFormGui.Models;

namespace CopaFormGui.Services;

public interface ISettingsService
{
    MachineSettings LoadSettings();
    void SaveSettings(MachineSettings settings);
    ConnectionSettings LoadConnectionSettings();
    void SaveConnectionSettings(ConnectionSettings settings);
}
