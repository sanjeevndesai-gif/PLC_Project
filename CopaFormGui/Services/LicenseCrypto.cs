using System.Globalization;
using CopaFormGui.Models;

namespace CopaFormGui.Services;

public static class LicenseCrypto
{
    // Replace with your production public key in PEM format.
    // Keep the matching private key only inside your dev licensing tool/workstation.
    public const string PublicKeyPem = @"-----BEGIN PUBLIC KEY-----
REPLACE_WITH_YOUR_RSA_PUBLIC_KEY
-----END PUBLIC KEY-----";

    public static string BuildPayload(LicenseFile license)
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