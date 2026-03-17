using System.Globalization;
using CopaFormGui.Models;

namespace CopaFormGui.Services;

public static class LicenseCrypto
{
    // Replace with your production public key in PEM format.
    // Keep the matching private key only inside your dev licensing tool/workstation.
    public const string PublicKeyPem = @"-----BEGIN PUBLIC KEY-----
MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEAyP+JzIjxR8SYDaFNqoCj
lk0pFivJlSZFNfJJHuZgFYvGY5boQ/UOLGpI/9vZbvHP2HXxOTa24pUmCrm+V7v5
n7GkdmG/F3BRJ8+PPlXhR8IOnrX9IVTRs5JBy4fPRbOr+SyrrN4m6fWZIB5Q5/mU
O6XQKBXo/Oj0OdfG+fh/8u5ztE1yQnydNEkWPtuirECx5G53EihOWiHGdzt9XZqB
QI3+xFlEEupTE/5b+XtXWMn31k5kEX62GfvRxOChAypajpfGr+vuaTCogeJByONs
9y+qzSKXgq3dHGg7PqnFsHF6HzYHcUVMhM2fTDexTBXvkZ6dsjpIl0gim965wmLc
PlpkSjRbXwbXea+uoPMt1BVjmUo94s0CJnukmRhXeYG3/6t2bUZBc4rHUZ9hnSg5
1CgMnjjwwVBfzDHVTZQphAg9FmUtdEJDfF0rjBvJ4bwiIidoVZGFih1x9fDILvXE
n93IlX+55x865HmtfXWsP09MKUsD6MBSroCHf33gOnMFAgMBAAE=
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