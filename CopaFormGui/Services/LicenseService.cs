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
    // Any license issued before this UTC timestamp is considered revoked after key rotation.
    private static readonly DateTime MinIssuedUtc = new(2026, 4, 2, 19, 57, 0, DateTimeKind.Utc);

    private static readonly string ProgramDataFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CopaFormGui");

    private static readonly string ProgramDataLicensePath = Path.Combine(ProgramDataFolder, "license.json");
    private static readonly string LocalLicensePath = Path.Combine(AppContext.BaseDirectory, "license.json");
    private static readonly string DebugLogPath = Path.Combine(AppContext.BaseDirectory, "license_debug.log");

    private static void LogDebug(string message)
    {
        try
        {
            var logMessage = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
            System.Diagnostics.Debug.WriteLine(message);
            File.AppendAllText(DebugLogPath, logMessage);
        }
        catch { }
    }

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
        var currentMachineId = GetCurrentMachineId();
        LogDebug($"\n===== ValidateCurrentMachineLicense started =====");
        LogDebug($"Current Machine ID: {currentMachineId}");
        
        var candidatePaths = new[] { ProgramDataLicensePath, LocalLicensePath };
        var checkedAny = false;
        var errors = new List<string>();

        foreach (var licensePath in candidatePaths)
        {
            LogDebug($"Checking path: {licensePath} - Exists: {File.Exists(licensePath)}");
            if (!File.Exists(licensePath))
                continue;

            checkedAny = true;
            if (TryValidateLicenseAtPath(licensePath, currentMachineId, out var errorMessage))
            {
                LogDebug($"✓ VALIDATION PASSED at: {licensePath}");
                return LicenseValidationResult.Ok();
            }

            errors.Add($"{licensePath}: {errorMessage}");
            LogDebug($"✗ VALIDATION FAILED at: {licensePath} - {errorMessage}");
        }

        if (!checkedAny)
        {
            var failMsg = $"License file not found.\nExpected: {ProgramDataLicensePath} or {LocalLicensePath}\n\n" +
                $"Machine ID: {currentMachineId}";
            LogDebug($"No license files found. Fail message: {failMsg}");
            return LicenseValidationResult.Fail(failMsg);
        }

        var finalMsg = "No valid license found.\n\n" + string.Join("\n", errors);
        LogDebug($"All licenses failed validation. Final error: {finalMsg}");
        return LicenseValidationResult.Fail(finalMsg);
    }

    private static bool TryValidateLicenseAtPath(string licensePath, string currentMachineId, out string errorMessage)
    {
        errorMessage = string.Empty;
        LogDebug($"[TryValidateLicenseAtPath] Validating license at: {licensePath}");
        LogDebug($"[TryValidateLicenseAtPath] Current machine ID: {currentMachineId}");

        LicenseFile? license;
        try
        {
            var json = File.ReadAllText(licensePath);
            license = JsonSerializer.Deserialize<LicenseFile>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            errorMessage = $"License file could not be read: {ex.Message}";
            LogDebug($"[TryValidateLicenseAtPath] ERROR: {errorMessage}");
            return false;
        }

        if (license is null)
        {
            errorMessage = "License file is empty or invalid.";
            LogDebug($"[TryValidateLicenseAtPath] ERROR: {errorMessage}");
            return false;
        }

        LogDebug($"[TryValidateLicenseAtPath] Product in license: {license.Product}");
        if (!string.Equals(license.Product, "CopaFormGui", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "License product does not match this application.";
            LogDebug($"[TryValidateLicenseAtPath] ERROR: {errorMessage}");
            return false;
        }

        LogDebug($"[TryValidateLicenseAtPath] Machine ID in license: {license.MachineId?.Trim()}");
        if (!string.Equals(license.MachineId?.Trim(), currentMachineId, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = $"License is not valid for this system. Licensed Machine ID: {license.MachineId}; Current Machine ID: {currentMachineId}";
            LogDebug($"[TryValidateLicenseAtPath] ERROR: {errorMessage}");
            return false;
        }

        LogDebug($"[TryValidateLicenseAtPath] Expires: {license.ExpiresUtc?.ToUniversalTime():O}, Now: {DateTime.UtcNow:O}");
        if (license.ExpiresUtc.HasValue && DateTime.UtcNow > license.ExpiresUtc.Value.ToUniversalTime())
        {
            errorMessage = "License has expired.";
            LogDebug($"[TryValidateLicenseAtPath] ERROR: {errorMessage}");
            return false;
        }

        LogDebug($"[TryValidateLicenseAtPath] IssuedUtc: {license.IssuedUtc.ToUniversalTime():O}, MinIssuedUtc: {MinIssuedUtc:O}");
        if (license.IssuedUtc.ToUniversalTime() < MinIssuedUtc)
        {
            errorMessage = "License was issued before the current key rotation and is no longer valid.";
            LogDebug($"[TryValidateLicenseAtPath] ERROR: {errorMessage}");
            return false;
        }

        LogDebug($"[TryValidateLicenseAtPath] Calling VerifySignature...");
        if (!VerifySignature(license))
        {
            errorMessage = "License signature is invalid.";
            LogDebug($"[TryValidateLicenseAtPath] ERROR: {errorMessage}");
            return false;
        }

        LogDebug($"[TryValidateLicenseAtPath] SUCCESS: License validated");
        return true;
    }

    private static bool VerifySignature(LicenseFile license)
    {
        try
        {
            if (LicenseCrypto.PublicKeyPem.Contains("REPLACE_WITH_YOUR_RSA_PUBLIC_KEY", StringComparison.Ordinal))
            {
                LogDebug("[LicenseService] ERROR: Public key contains placeholder text!");
                return false;
            }

            var payload = LicenseCrypto.BuildPayload(license);
            LogDebug($"[VerifySignature] Payload: {payload}");
            LogDebug($"[VerifySignature] Public key first 60 chars: {LicenseCrypto.PublicKeyPem.Substring(0, 60)}");
            LogDebug($"[VerifySignature] Signature length: {license.Signature.Length}");

            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            var signatureBytes = Convert.FromBase64String(license.Signature);

            using var rsa = ImportPublicKeyFromPem(LicenseCrypto.PublicKeyPem);
            var result = rsa.VerifyData(payloadBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            LogDebug($"[VerifySignature] Result: {result}");
            return result;
        }
        catch (Exception ex)
        {
            LogDebug($"[VerifySignature] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            LogDebug($"[VerifySignature] Stack trace: {ex.StackTrace}");
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
        try
        {
            var base64 = pem
                .Replace("-----BEGIN PUBLIC KEY-----", "")
                .Replace("-----END PUBLIC KEY-----", "")
                .Replace("\r", "").Replace("\n", "").Trim();
            var spki = Convert.FromBase64String(base64);
            LogDebug($"[ImportPublicKeyFromPem] DER SPKI length: {spki.Length} bytes");

            // Parse DER SubjectPublicKeyInfo to extract RSA modulus and exponent
            int i = 0;
            DerAdvancePast(spki, ref i, 0x30); // outer SEQUENCE
            LogDebug($"[ImportPublicKeyFromPem] Parsed outer SEQUENCE");
            DerSkipNode(spki, ref i, 0x30);    // algorithm SEQUENCE (skip entirely)
            LogDebug($"[ImportPublicKeyFromPem] Skipped algorithm SEQUENCE");
            DerAdvancePast(spki, ref i, 0x03); // BIT STRING
            i++;                               // skip unused-bits byte (0x00)
            LogDebug($"[ImportPublicKeyFromPem] Parsed BIT STRING");
            DerAdvancePast(spki, ref i, 0x30); // RSAPublicKey SEQUENCE
            LogDebug($"[ImportPublicKeyFromPem] Parsed RSAPublicKey SEQUENCE");
            var modulus  = DerReadInteger(spki, ref i);
            LogDebug($"[ImportPublicKeyFromPem] Parsed modulus: {modulus.Length} bytes");
            var exponent = DerReadInteger(spki, ref i);
            LogDebug($"[ImportPublicKeyFromPem] Parsed exponent: {exponent.Length} bytes");

            var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters { Modulus = modulus, Exponent = exponent });
            LogDebug($"[ImportPublicKeyFromPem] RSA key imported successfully");
            return rsa;
        }
        catch (Exception ex)
        {
            LogDebug($"[ImportPublicKeyFromPem] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            LogDebug($"[ImportPublicKeyFromPem] Stack trace: {ex.StackTrace}");
            throw;
        }
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