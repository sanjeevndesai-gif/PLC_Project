namespace CopaFormGui.Services;

public interface ILicenseService
{
    string GetMachineId();
    bool IsLicenseValid();
    bool ActivateLicense(string licenseKey);
}
