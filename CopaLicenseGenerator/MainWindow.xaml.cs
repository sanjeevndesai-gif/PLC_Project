using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace CopaLicenseGenerator;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        LoadCurrentMachineId();
        LoadDefaultPrivateKey();
    }

    private void LoadDefaultPrivateKey()
    {
        // Look for the private key next to the EXE first (published/installed), then in project keys\ folder
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "keys", "dev_private_key.pem"),
            Path.Combine(AppContext.BaseDirectory, "dev_private_key.pem"),
            Path.Combine(AppContext.BaseDirectory, "..", "keys", "dev_private_key.pem"),
        };

        foreach (var path in candidates)
        {
            var full = Path.GetFullPath(path);
            if (File.Exists(full))
            {
                PrivateKeyPathTextBox.Text = full;
                return;
            }
        }
    }

    private void UseThisMachineId_Click(object sender, RoutedEventArgs e)
    {
        LoadCurrentMachineId();
    }

    private void BrowsePrivateKey_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Private Key PEM",
            Filter = "PEM files (*.pem)|*.pem|All files (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
            PrivateKeyPathTextBox.Text = dlg.FileName;
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Save License JSON",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = "license.json"
        };

        if (dlg.ShowDialog() == true)
            OutputPathTextBox.Text = dlg.FileName;
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        // Show password dialog
        var pwdDialog = new PasswordDialog { Owner = this };
        if (pwdDialog.ShowDialog() != true)
            return;

        // Hardcoded password for demonstration (replace with secure storage for production)
        const string requiredPassword = "Sanpug@260128";
        if (pwdDialog.EnteredPassword != requiredPassword)
        {
            MessageBox.Show(this, "Incorrect password.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            var request = BuildRequest();
            var outputPath = LicenseGeneratorService.GenerateLicense(request);

            var statusLines = new System.Text.StringBuilder();
            statusLines.AppendLine("License generated successfully.");
            statusLines.AppendLine();
            statusLines.AppendLine("Saved to: " + outputPath);

            // Always copy to C:\ProgramData\CopaFormGui\ (create folder if needed)
            var programDataTarget = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "CopaFormGui");

            Directory.CreateDirectory(programDataTarget);
            var destPath = Path.Combine(programDataTarget, "license.json");
            File.Copy(outputPath, destPath, overwrite: true);
            statusLines.AppendLine();
            statusLines.AppendLine("Deployed to: " + destPath);

            StatusTextBox.Text = statusLines.ToString();
            MessageBox.Show(this, StatusTextBox.Text, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusTextBox.Text = "Failed to generate license.\n\n" + ex.Message;
            MessageBox.Show(this, ex.Message, "Generate Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private LicenseGenerationRequest BuildRequest()
    {
        var machineId = RequireText(MachineIdTextBox.Text, "Machine ID");
        var customer = RequireText(CustomerTextBox.Text, "Customer Name");
        var product = RequireText(ProductTextBox.Text, "Product");
        var privateKeyPath = RequireText(PrivateKeyPathTextBox.Text, "Private Key PEM Path");
        var outputPath = RequireText(OutputPathTextBox.Text, "Output Path");

        DateTime? expiresUtc = null;
        var expiresText = ExpiresUtcTextBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(expiresText))
        {
            if (!DateTime.TryParse(expiresText, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed))
                throw new InvalidOperationException("Invalid Expires UTC. Use ISO format, for example: 2027-12-31T23:59:59Z");

            expiresUtc = parsed.ToUniversalTime();
        }

        return new LicenseGenerationRequest
        {
            Product = product,
            CustomerName = customer,
            MachineId = machineId,
            PrivateKeyPath = privateKeyPath,
            OutputPath = outputPath,
            ExpiresUtc = expiresUtc
        };
    }

    private static string RequireText(string? value, string fieldName)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException(fieldName + " is required.");

        return trimmed;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void LoadCurrentMachineId()
    {
        var machineId = GetCurrentMachineId();
        MachineIdTextBox.Text = machineId;

        if (string.IsNullOrWhiteSpace(StatusTextBox.Text))
            StatusTextBox.Text = "Machine ID auto-detected from this PC.";
    }

    private static string GetCurrentMachineId()
    {
        var machineGuid = GetWindowsMachineGuid();
        if (!string.IsNullOrWhiteSpace(machineGuid))
            return machineGuid.ToUpperInvariant();

        var fallback = $"{Environment.MachineName}|{Environment.UserDomainName}|{Environment.OSVersion.VersionString}";
        return ComputeSha256Hex(fallback);
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
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
