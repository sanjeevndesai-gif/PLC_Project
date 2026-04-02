using System.Globalization;
using CopaFormGui.Models;

namespace CopaFormGui.Services;

public static class LicenseCrypto
{
    // Replace with your production public key in PEM format.
    // Keep the matching private key only inside your dev licensing tool/workstation.
    public const string PublicKeyPem = @"-----BEGIN PUBLIC KEY-----
MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEAmzTxQ7oSVdqgtPfsvYHT
YCLGYzgfI1z7pMAMoIG95d5y3wzlDvdDY0uUSgRihh2zukSNvdx+z5qTOI3LYZ4O
7/F3JBYxQP57W/gaVcz7J56+z5P3iIygcMDtavZIq2TLXn9oI9G7MQnkgCmLFKAo
jFBb+MX1hDTo0yj+723EdfSOnwf7bY2TyFYNIFc/vRvAkKogCdyeRQu7nNtti1Hg
2hTWkgUbg2PHu8NjroaBp9NS11hYjk1+KZ3BrKgV4fMZM8PSoyX1NqlNcdFBhDk2
/XvDz/L2KXl5UncXM0ge9LxKj+IWNd9ePjTvJa4jE7gVzH4Y820DdcVdgFrevnKJ
kP/6ojc4N3oUaAa3BmGF4zEERXfrEg8ljoEYPn1krVb89rJ+dOqT1PxTkqZXmMLw
3tayl+ZEAeKfDm0JXYCzdWLk48mWVEOjN7EXicFejvEclCb2I1rD1S6N0hQbTqgt
9x20/+C8tzn9tAsdGHgXcQEa5pNflx1tvVzDWnEQw6gZAgMBAAE=
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