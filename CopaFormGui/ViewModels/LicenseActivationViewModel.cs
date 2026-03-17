using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public partial class LicenseActivationViewModel : ObservableObject
{
    private readonly ILicenseService _licenseService;

    [ObservableProperty]
    private string _machineId = string.Empty;

    [ObservableProperty]
    private string _licenseKey = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _isActivated;

    public event EventHandler? ActivationCompleted;

    public LicenseActivationViewModel(ILicenseService licenseService)
    {
        _licenseService = licenseService;
        MachineId = _licenseService.GetMachineId();
    }

    [RelayCommand(CanExecute = nameof(CanActivate))]
    private void Activate()
    {
        HasError = false;
        StatusMessage = string.Empty;

        if (_licenseService.ActivateLicense(LicenseKey))
        {
            IsActivated = true;
            StatusMessage = "License activated successfully.";
            ActivationCompleted?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            HasError = true;
            StatusMessage = "Invalid license key. Please enter the key provided for this Machine ID.";
        }
    }

    private bool CanActivate() => !string.IsNullOrWhiteSpace(LicenseKey);

    partial void OnLicenseKeyChanged(string value) => ActivateCommand.NotifyCanExecuteChanged();
}
