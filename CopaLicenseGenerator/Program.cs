using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var argsMap = ParseArgs(args);

if (HasArg(argsMap, "help") || args.Length == 0)
{
    PrintUsage();
    return 0;
}

var machineId = GetRequired(argsMap, "machine-id");
var customer = GetRequired(argsMap, "customer");
var outPath = GetRequired(argsMap, "out");
var privateKeyPath = GetRequired(argsMap, "private-key");
var product = GetOptional(argsMap, "product", "CopaFormGui") ?? "CopaFormGui";

DateTime? expiresUtc = null;
var expiresText = GetOptional(argsMap, "expires-utc", null);
if (!string.IsNullOrWhiteSpace(expiresText))
{
    if (!DateTime.TryParse(expiresText, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed))
    {
        Console.Error.WriteLine("Invalid --expires-utc. Use ISO-8601 format, e.g. 2027-12-31T23:59:59Z");
        return 2;
    }

    expiresUtc = parsed.ToUniversalTime();
}

if (!File.Exists(privateKeyPath))
{
    Console.Error.WriteLine($"Private key file not found: {privateKeyPath}");
    return 2;
}

var issuedUtc = DateTime.UtcNow;
var license = new LicenseFile
{
    Product = product,
    CustomerName = customer,
    MachineId = machineId.ToUpperInvariant(),
    IssuedUtc = issuedUtc,
    ExpiresUtc = expiresUtc
};

var privateKeyPem = File.ReadAllText(privateKeyPath);
license.Signature = CreateSignature(license, privateKeyPem);

var json = JsonSerializer.Serialize(license, new JsonSerializerOptions { WriteIndented = true });

var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outPath));
if (!string.IsNullOrWhiteSpace(outputDirectory) && !Directory.Exists(outputDirectory))
    Directory.CreateDirectory(outputDirectory);

File.WriteAllText(outPath, json);
Console.WriteLine($"License created: {Path.GetFullPath(outPath)}");
return 0;

static string CreateSignature(LicenseFile license, string privateKeyPem)
{
    var payload = BuildPayload(license);
    var payloadBytes = Encoding.UTF8.GetBytes(payload);

    using var rsa = RSA.Create();
    rsa.ImportFromPem(privateKeyPem);
    var signature = rsa.SignData(payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    return Convert.ToBase64String(signature);
}

static string BuildPayload(LicenseFile license)
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

static Dictionary<string, string> ParseArgs(string[] args)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (!arg.StartsWith("--", StringComparison.Ordinal))
            continue;

        var key = arg[2..];
        var value = "true";
        if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            value = args[i + 1];
            i++;
        }

        map[key] = value;
    }

    return map;
}

static bool HasArg(Dictionary<string, string> argsMap, string key)
{
    return argsMap.ContainsKey(key);
}

static string GetRequired(Dictionary<string, string> argsMap, string key)
{
    if (argsMap.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        return value;

    Console.Error.WriteLine($"Missing required argument --{key}");
    PrintUsage();
    Environment.Exit(2);
    return string.Empty;
}

static string? GetOptional(Dictionary<string, string> argsMap, string key, string? defaultValue)
{
    return argsMap.TryGetValue(key, out var value) ? value : defaultValue;
}

static void PrintUsage()
{
    Console.WriteLine("CopaLicenseGenerator");
    Console.WriteLine("Creates a signed machine-locked license file for CopaFormGui.");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project CopaLicenseGenerator -- \\");
    Console.WriteLine("    --machine-id <ID> \\");
    Console.WriteLine("    --customer <NAME> \\");
    Console.WriteLine("    --private-key <PRIVATE_KEY_PEM_PATH> \\");
    Console.WriteLine("    --out <LICENSE_JSON_PATH> [--expires-utc <ISO_UTC>] [--product CopaFormGui]");
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