using System.IO;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CopaFormGui.Models;

namespace CopaFormGui.Services;

public class LicenseService : ILicenseService
{
    private static readonly string LicenseFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CopaFormGui");

    private static readonly string LicensePath = Path.Combine(LicenseFolder, "license.json");

    // Internal secret used for HMAC key derivation — not a user-visible secret
    private static readonly byte[] _hmacSeed = Encoding.UTF8.GetBytes("CopaFormGui-License-v1");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string GetMachineId()
    {
        try
        {
            var components = new List<string>();

            using var mbQuery = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
            var mbObjects = mbQuery.Get();
            foreach (ManagementObject obj in mbObjects)
            {
                components.Add(obj["SerialNumber"]?.ToString() ?? string.Empty);
                break; // take only the first entry
            }

            using var cpuQuery = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
            var cpuObjects = cpuQuery.Get();
            foreach (ManagementObject obj in cpuObjects)
            {
                components.Add(obj["ProcessorId"]?.ToString() ?? string.Empty);
                break; // take only the first entry
            }

            var raw = string.Join("|", components);
            if (string.IsNullOrWhiteSpace(raw))
                raw = Environment.MachineName;

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash)[..16];
        }
        catch
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Environment.MachineName));
            return Convert.ToHexString(hash)[..16];
        }
    }

    public bool IsLicenseValid()
    {
        var info = LoadLicense();
        if (info is null || !info.IsActivated || string.IsNullOrEmpty(info.LicenseKey))
            return false;

        return ValidateKey(info.MachineId, info.LicenseKey);
    }

    public bool ActivateLicense(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return false;

        var machineId = GetMachineId();
        if (!ValidateKey(machineId, licenseKey))
            return false;

        EnsureFolderExists();
        var info = new LicenseInfo
        {
            MachineId = machineId,
            LicenseKey = licenseKey,
            IsActivated = true,
            ActivatedAt = DateTime.UtcNow
        };
        File.WriteAllText(LicensePath, JsonSerializer.Serialize(info, JsonOptions));
        return true;
    }

    private static bool ValidateKey(string machineId, string licenseKey)
    {
        var expected = ComputeExpectedKey(machineId);
        return string.Equals(expected, licenseKey.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    internal static string ComputeExpectedKey(string machineId)
    {
        // internal visibility is intentional: enables integration testing without exposing a public API
        using var hmac = new HMACSHA256(_hmacSeed);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(machineId));
        return Convert.ToHexString(hash)[..24];
    }

    private static LicenseInfo? LoadLicense()
    {
        try
        {
            if (File.Exists(LicensePath))
            {
                var json = File.ReadAllText(LicensePath);
                return JsonSerializer.Deserialize<LicenseInfo>(json);
            }
        }
        catch { /* Treat as unlicensed on error */ }
        return null;
    }

    private static void EnsureFolderExists()
    {
        if (!Directory.Exists(LicenseFolder))
            Directory.CreateDirectory(LicenseFolder);
    }
}
