using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using CopaFormGui.Models;

namespace CopaFormGui.Services;

public class LicenseService : ILicenseService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly string ProgramDataFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CopaFormGui");

    private static readonly string ProgramDataLicensePath = Path.Combine(ProgramDataFolder, "license.json");
    private static readonly string LocalLicensePath = Path.Combine(AppContext.BaseDirectory, "license.json");

    public string GetCurrentMachineId()
    {
        var machineGuid = GetWindowsMachineGuid();
        if (!string.IsNullOrWhiteSpace(machineGuid))
            return machineGuid.ToUpperInvariant();

        var fallback = $"{Environment.MachineName}|{Environment.UserDomainName}|{Environment.OSVersion.VersionString}";
        return ComputeSha256Hex(fallback);
    }

    public LicenseValidationResult ValidateCurrentMachineLicense()
    {
        var licensePath = File.Exists(ProgramDataLicensePath) ? ProgramDataLicensePath : LocalLicensePath;
        var currentMachineId = GetCurrentMachineId();

        if (!File.Exists(licensePath))
        {
            return LicenseValidationResult.Fail(
                $"License file not found.\nExpected: {ProgramDataLicensePath} or {LocalLicensePath}\n\n" +
                $"Machine ID: {currentMachineId}");
        }

        LicenseFile? license;
        try
        {
            var json = File.ReadAllText(licensePath);
            license = JsonSerializer.Deserialize<LicenseFile>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            return LicenseValidationResult.Fail($"License file could not be read: {ex.Message}");
        }

        if (license is null)
            return LicenseValidationResult.Fail("License file is empty or invalid.");

        if (!string.Equals(license.Product, "CopaFormGui", StringComparison.OrdinalIgnoreCase))
            return LicenseValidationResult.Fail("License product does not match this application.");

        if (!string.Equals(license.MachineId?.Trim(), currentMachineId, StringComparison.OrdinalIgnoreCase))
        {
            return LicenseValidationResult.Fail(
                $"License is not valid for this system.\nLicensed Machine ID: {license.MachineId}\nCurrent Machine ID: {currentMachineId}");
        }

        if (license.ExpiresUtc.HasValue && DateTime.UtcNow > license.ExpiresUtc.Value.ToUniversalTime())
            return LicenseValidationResult.Fail("License has expired.");

        if (!VerifySignature(license))
            return LicenseValidationResult.Fail("License signature is invalid.");

        return LicenseValidationResult.Ok();
    }

    private static bool VerifySignature(LicenseFile license)
    {
        try
        {
            if (LicenseCrypto.PublicKeyPem.Contains("REPLACE_WITH_YOUR_RSA_PUBLIC_KEY", StringComparison.Ordinal))
                return false;

            var payload = LicenseCrypto.BuildPayload(license);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            var signatureBytes = Convert.FromBase64String(license.Signature);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(LicenseCrypto.PublicKeyPem);
            return rsa.VerifyData(payloadBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    private static string? GetWindowsMachineGuid()
    {
        try
        {
            using var localMachineX64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var cryptographyKey = localMachineX64.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return cryptographyKey?.GetValue("MachineGuid")?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string ComputeSha256Hex(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}