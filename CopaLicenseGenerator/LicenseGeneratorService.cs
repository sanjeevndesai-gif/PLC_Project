using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CopaLicenseGenerator;

public sealed class LicenseGenerationRequest
{
    public string Product { get; init; } = "CopaFormGui";
    public string CustomerName { get; init; } = string.Empty;
    public string MachineId { get; init; } = string.Empty;
    public string PrivateKeyPath { get; init; } = string.Empty;
    public string OutputPath { get; init; } = string.Empty;
    public DateTime? ExpiresUtc { get; init; }
}

internal sealed class LicenseFile
{
    public string Product { get; set; } = "CopaFormGui";
    public string CustomerName { get; set; } = string.Empty;
    public string MachineId { get; set; } = string.Empty;
    public DateTime IssuedUtc { get; set; }
    public DateTime? ExpiresUtc { get; set; }
    public string Signature { get; set; } = string.Empty;
}

public static class LicenseGeneratorService
{
    public static string GenerateLicense(LicenseGenerationRequest request)
    {
        if (!File.Exists(request.PrivateKeyPath))
            throw new InvalidOperationException("Private key file not found: " + request.PrivateKeyPath);

        var issuedUtc = DateTime.UtcNow;
        var license = new LicenseFile
        {
            Product = request.Product.Trim(),
            CustomerName = request.CustomerName.Trim(),
            MachineId = request.MachineId.Trim().ToUpperInvariant(),
            IssuedUtc = issuedUtc,
            ExpiresUtc = request.ExpiresUtc
        };

        var privateKeyPem = File.ReadAllText(request.PrivateKeyPath);
        license.Signature = CreateSignature(license, privateKeyPem);

        var json = JsonSerializer.Serialize(license, new JsonSerializerOptions { WriteIndented = true });

        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(request.OutputPath));
        if (!string.IsNullOrWhiteSpace(outputDirectory) && !Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        File.WriteAllText(request.OutputPath, json);
        return Path.GetFullPath(request.OutputPath);
    }

    private static string CreateSignature(LicenseFile license, string privateKeyPem)
    {
        var payload = BuildPayload(license);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var signature = rsa.SignData(payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signature);
    }

    private static string BuildPayload(LicenseFile license)
    {
        return string.Join("|", new[]
        {
            license.Product.Trim(),
            license.CustomerName.Trim(),
            license.MachineId.Trim().ToUpperInvariant(),
            license.IssuedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            license.ExpiresUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty
        });
    }
}
