namespace CopaFormGui.Services;

public sealed class LicenseValidationResult
{
    public static LicenseValidationResult Ok() => new() { IsValid = true };

    public static LicenseValidationResult Fail(string errorMessage) => new()
    {
        IsValid = false,
        ErrorMessage = errorMessage
    };

    public bool IsValid { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
}