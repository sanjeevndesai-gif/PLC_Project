namespace CopaFormGui.Services;

public interface ILicenseService
{
    string GetCurrentMachineId();
    LicenseValidationResult ValidateCurrentMachineLicense();
}