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
            // Diagnostic: Show which path is being checked
            System.Windows.MessageBox.Show($"Checked for license at:\n{ProgramDataLicensePath}\nand\n{LocalLicensePath}", "License Check Diagnostic");
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

            using var rsa = ImportPublicKeyFromPem(LicenseCrypto.PublicKeyPem);
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
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }

    private static RSA ImportPublicKeyFromPem(string pem)
    {
        var base64 = pem
            .Replace("-----BEGIN PUBLIC KEY-----", "")
            .Replace("-----END PUBLIC KEY-----", "")
            .Replace("\r", "").Replace("\n", "").Trim();
        var spki = Convert.FromBase64String(base64);

        // Parse DER SubjectPublicKeyInfo to extract RSA modulus and exponent
        int i = 0;
        DerAdvancePast(spki, ref i, 0x30); // outer SEQUENCE
        DerSkipNode(spki, ref i, 0x30);    // algorithm SEQUENCE (skip entirely)
        DerAdvancePast(spki, ref i, 0x03); // BIT STRING
        i++;                               // skip unused-bits byte (0x00)
        DerAdvancePast(spki, ref i, 0x30); // RSAPublicKey SEQUENCE
        var modulus  = DerReadInteger(spki, ref i);
        var exponent = DerReadInteger(spki, ref i);

        var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters { Modulus = modulus, Exponent = exponent });
        return rsa;
    }

    private static void DerAdvancePast(byte[] data, ref int i, byte tag)
    {
        if (data[i] != tag) throw new CryptographicException($"DER parse: expected 0x{tag:X2} got 0x{data[i]:X2}");
        i++;
        DerReadLength(data, ref i); // consume length bytes; i now at content
    }

    private static void DerSkipNode(byte[] data, ref int i, byte tag)
    {
        if (data[i] != tag) throw new CryptographicException($"DER parse: expected 0x{tag:X2} got 0x{data[i]:X2}");
        i++;
        int len = DerReadLength(data, ref i);
        i += len; // skip content
    }

    private static int DerReadLength(byte[] data, ref int i)
    {
        int first = data[i++];
        if ((first & 0x80) == 0) return first;
        int count = first & 0x7F;
        int len = 0;
        for (int k = 0; k < count; k++) len = (len << 8) | data[i++];
        return len;
    }

    private static byte[] DerReadInteger(byte[] data, ref int i)
    {
        if (data[i] != 0x02) throw new CryptographicException($"DER parse: expected INTEGER (0x02) got 0x{data[i]:X2}");
        i++;
        int len = DerReadLength(data, ref i);
        if (len > 1 && data[i] == 0x00) { i++; len--; } // strip positive-indicator leading zero
        var result = new byte[len];
        Array.Copy(data, i, result, 0, len);
        i += len;
        return result;
    }
}