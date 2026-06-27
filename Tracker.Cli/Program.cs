using Serilog;
using System.Diagnostics;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using Tracker.Shared.Utilities;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Tracker",
        "logs",
        "service-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    // If there are command line arguments, run the command and exit
    if (args.Length > 0)
    {
        await RunCommandLine(args);
        return;
    }

    // No arguments - show help and exit (don't start service)
    Log.Warning("⚠️ No command arguments provided");
    Log.Information("📋 Showing help menu...");
    ShowHelp();
    Log.Information("ℹ️  Usage: Tracker.Cli.exe <command> [options]");
    Log.Information("ℹ️  Run 'Tracker.Cli.exe help' for more information");
    Console.WriteLine();

    return;
}
catch (Exception ex)
{
    Log.Fatal(ex, "💀 Application crashed");
}
finally
{
    Log.CloseAndFlush();
}

// ---------------- COMMAND LINE HANDLING ----------------
static async Task RunCommandLine(string[] args)
{
    ShowDisclaimer();

    var command = args[0].ToLower();
    var arguments = args.Skip(1).ToArray();

    switch (command)
    {
        case "install":
            await InstallService();
            break;
        case "uninstall":
            await UninstallService();
            break;
        case "start":
            await StartService();
            break;
        case "stop":
            await StopService();
            break;
        case "restart":
            await RestartService();
            break;
        case "status":
            ShowStatus();
            break;
        case "config":
            ShowConfig();
            break;
        case "set":
            await SetConfig(arguments);
            break;
        case "run-once":
            await RunOnce();
            break;
        case "test":
            await TestConfig();
            break;
        case "version":
            ShowVersion();
            break;
        case "help":
        default:
            ShowHelp();
            break;
    }
}

static void ShowDisclaimer()
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║  ⚠️  DISCLAIMER                                                ║");
    Console.WriteLine("║  This software is for EDUCATIONAL and ETHICAL use only.        ║");
    Console.WriteLine("║  Only use on devices you own or have explicit permission.      ║");
    Console.WriteLine("║  The author assumes NO responsibility for misuse or damages.   ║");
    Console.WriteLine("║  By using this software, you agree to comply with all laws.    ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
    Console.ResetColor();
    Console.WriteLine();
}

static void ShowHelp()
{
    Console.WriteLine(@"
Laptop Tracker - Device monitoring and reporting tool

⚠️  IMPORTANT: This is the CLI management tool (Tracker.Cli.exe).
    The actual Windows Service is Tracker.Service.exe

Usage: Tracker.Cli.exe <command> [options]

Service Commands:
  install              Install the Windows service (requires admin)
  uninstall            Uninstall the Windows service (requires admin)
  start                Start the Windows service (requires admin)
  stop                 Stop the Windows service (requires admin)
  restart              Restart the Windows service (requires admin)
  status               Check service status

Configuration Commands:
  config               View current configuration
  set webhook <url>    Set webhook URL
  set interval <sec>   Set reporting interval (minimum 30s)

Diagnostic Commands:
  run-once             Run a single report (foreground)
  test                 Test configuration and connection
  version              Display version information
  help                 Show this help message

Examples:
  Tracker.Cli.exe install                  # Install the service
  Tracker.Cli.exe start                    # Start the service
  Tracker.Cli.exe status                   # Check if service is running
  Tracker.Cli.exe set webhook https://example.com/api/report
  Tracker.Cli.exe set interval 180
  Tracker.Cli.exe run-once                 # Test a single report

💡  Run as Administrator for install/uninstall/start/stop commands");
    Console.WriteLine();
}

static bool IsAdministrator()
{
    var identity = WindowsIdentity.GetCurrent();
    var principal = new WindowsPrincipal(identity);
    return principal.IsInRole(WindowsBuiltInRole.Administrator);
}

static async Task InstallService()
{
    Log.Information("🔧 Installing LaptopTracker service...");

    try
    {
        // Check if running as administrator
        if (!IsAdministrator())
        {
            Log.Error("❌ Administrator privileges required to install service");
            Log.Information("💡 Please run this command as Administrator");
            Log.Information("💡 Right-click Command Prompt and select 'Run as administrator'");
            return;
        }

        // Get the service executable path (same directory as CLI)
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        var serviceExe = Path.Combine(exeDir, "Tracker.Service.exe");

        if (!File.Exists(serviceExe))
        {
            Log.Error("❌ Tracker.Service.exe not found at: {Path}", serviceExe);
            Log.Information("💡 Make sure Tracker.Service is built and in the same directory");
            Log.Information("💡 Build Tracker.Service project first, then run Tracker.Cli.exe");
            return;
        }

        Log.Information("📁 Found service executable at: {Path}", serviceExe);

        // Check if service already exists
        try
        {
            using var sc = new ServiceController("LaptopTracker");
            var status = sc.Status;
            Log.Warning("⚠️ Service already exists. Please uninstall first.");
            Log.Information("ℹ️  Use 'Tracker.Cli.exe uninstall' to remove it");
            return;
        }
        catch
        {
            // Service doesn't exist - continue with installation
        }

        // Create the service using sc.exe
        var psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"create LaptopTracker binPath= \"{serviceExe}\" start= auto displayname= \"Laptop Tracker\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        Log.Information("🔧 Creating Windows Service...");

        using var process = Process.Start(psi);
        if (process != null)
        {
            await process.WaitForExitAsync();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            if (process.ExitCode == 0)
            {
                Log.Information("✅ Service installed successfully");

                // Set service description
                var descPsi = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"description LaptopTracker \"Device monitoring and reporting service\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var descProcess = Process.Start(descPsi);
                if (descProcess != null)
                {
                    await descProcess.WaitForExitAsync();
                }

                Log.Information("📋 Service: Laptop Tracker");
                Log.Information("📋 Status: Installed");
                Log.Information("📋 Start Type: Automatic");
                Log.Information("");
                Log.Information("ℹ️  Next steps:");
                Log.Information("  1. Configure webhook: Tracker.Cli.exe set webhook <url>");
                Log.Information("  2. Start the service: Tracker.Cli.exe start");
                Log.Information("  3. Check status: Tracker.Cli.exe status");
            }
            else
            {
                Log.Error("❌ Failed to install service (Exit Code: {ExitCode})", process.ExitCode);
                if (!string.IsNullOrEmpty(error))
                    Log.Error("📋 Error: {Error}", error.Trim());
                if (!string.IsNullOrEmpty(output))
                    Log.Information("📋 Output: {Output}", output.Trim());

                Log.Information("💡 Try running: sc create LaptopTracker binPath= \"{Path}\" start= auto", serviceExe);
            }
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "❌ Failed to install service");
        Log.Information("💡 Try running as Administrator");
    }
}

static async Task UninstallService()
{
    Log.Information("🔧 Uninstalling LaptopTracker service...");

    try
    {
        // Check if running as administrator
        if (!IsAdministrator())
        {
            Log.Error("❌ Administrator privileges required to uninstall service");
            Log.Information("💡 Please run this command as Administrator");
            return;
        }

        // Check if service exists
        try
        {
            using var sc = new ServiceController("LaptopTracker");
            // Service exists, continue
        }
        catch
        {
            Log.Warning("⚠️ Service 'LaptopTracker' not found");
            return;
        }

        // Stop the service if running
        await StopService();
        await Task.Delay(1000);

        // Delete the service
        var psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = "delete LaptopTracker",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi);
        if (process != null)
        {
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                Log.Information("✅ Service uninstalled successfully");

                // Ask if user wants to remove configuration
                Console.Write("Remove configuration files? (y/n): ");
                var response = Console.ReadLine()?.ToLower();
                if (response == "y" || response == "yes")
                {
                    var configDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "Tracker");
                    if (Directory.Exists(configDir))
                    {
                        Directory.Delete(configDir, true);
                        Log.Information("🗑️ Configuration files removed");
                    }
                }
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync();
                Log.Error("❌ Failed to uninstall service: {Error}", error);
            }
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "❌ Failed to uninstall service");
    }
}

static async Task StartService()
{
    Log.Information("▶️ Starting LaptopTracker service...");

    try
    {
        // Check if running as administrator
        if (!IsAdministrator())
        {
            Log.Error("❌ Administrator privileges required to start service");
            Log.Information("💡 Please run this command as Administrator");
            return;
        }

        using var sc = new ServiceController("LaptopTracker");

        if (sc.Status == ServiceControllerStatus.Running)
        {
            Log.Warning("⚠️ Service is already running");
            return;
        }

        sc.Start();
        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
        Log.Information("✅ Service started successfully");
        Log.Information("ℹ️  Service is now monitoring and reporting");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "❌ Failed to start service");
        Log.Information("💡 Tip: Make sure the service is installed and run as administrator");
        Log.Information("💡 Try: Tracker.Cli.exe install first");
    }
}

static async Task StopService()
{
    Log.Information("⏹️ Stopping LaptopTracker service...");

    try
    {
        using var sc = new ServiceController("LaptopTracker");

        if (sc.Status == ServiceControllerStatus.Stopped)
        {
            Log.Warning("⚠️ Service is already stopped");
            return;
        }

        sc.Stop();
        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
        Log.Information("✅ Service stopped successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "❌ Failed to stop service");
    }
}

static async Task RestartService()
{
    Log.Information("🔄 Restarting LaptopTracker service...");
    await StopService();
    await Task.Delay(2000);
    await StartService();
}

static void ShowStatus()
{
    try
    {
        using var sc = new ServiceController("LaptopTracker");

        Console.WriteLine($"Service: {sc.DisplayName}");
        Console.WriteLine($"Status: {sc.Status}");
        Console.WriteLine($"Start Type: {sc.StartType}");

        // Show additional info based on status
        Console.WriteLine();
        switch (sc.Status)
        {
            case ServiceControllerStatus.Running:
                Console.WriteLine("✅ Service is running and monitoring");
                break;
            case ServiceControllerStatus.Stopped:
                Console.WriteLine("⏹️ Service is stopped");
                Console.WriteLine("💡 Start it with: Tracker.Cli.exe start");
                break;
            case ServiceControllerStatus.StartPending:
                Console.WriteLine("⏳ Service is starting...");
                break;
            case ServiceControllerStatus.StopPending:
                Console.WriteLine("⏳ Service is stopping...");
                break;
            case ServiceControllerStatus.Paused:
                Console.WriteLine("⏸️ Service is paused");
                break;
        }
    }
    catch
    {
        Console.WriteLine("❌ Service 'LaptopTracker' not found");
        Console.WriteLine("💡 Tip: Install the service with 'Tracker.Cli.exe install'");
    }
}

static void ShowConfig()
{
    var config = ConfigurationManager.LoadConfig();
    Console.WriteLine(@$"
Current Configuration:
  Webhook URL: {config.WebhookUrl}
  Interval: {config.ReportIntervalSeconds} seconds
  Retry Count: {config.RetryCount}
  Retry Delay: {config.RetryDelaySeconds} seconds
  Geo Location: {(config.EnableGeoLocation ? "✅ Enabled" : "❌ Disabled")}
  Battery Monitoring: {(config.EnableBatteryMonitoring ? "✅ Enabled" : "❌ Disabled")}
  Disk Monitoring: {(config.EnableDiskMonitoring ? "✅ Enabled" : "❌ Disabled")}
");
}

static async Task SetConfig(string[] args)
{
    if (args.Length < 2)
    {
        Log.Error("❌ Invalid syntax. Use: Tracker.Cli.exe set <key> <value>");
        Log.Information("ℹ️  Valid keys: webhook, interval");
        return;
    }

    var config = ConfigurationManager.LoadConfig();
    var key = args[0].ToLower();
    var value = args[1];

    switch (key)
    {
        case "webhook":
            config.WebhookUrl = value;
            ConfigurationManager.SaveConfig(config);
            Log.Information("✅ Webhook URL set to: {Url}", value);
            Log.Information("ℹ️  Restart service for changes to take effect: Tracker.Cli.exe restart");
            break;

        case "interval":
            if (int.TryParse(value, out var seconds))
            {
                seconds = Math.Max(30, seconds);
                config.ReportIntervalSeconds = seconds;
                ConfigurationManager.SaveConfig(config);
                Log.Information("✅ Reporting interval set to {Seconds} seconds", seconds);
                Log.Information("ℹ️  Restart service for changes to take effect: Tracker.Cli.exe restart");
            }
            else
            {
                Log.Error("❌ Invalid interval. Please specify a number in seconds.");
                Log.Information("ℹ️  Example: Tracker.Cli.exe set interval 180");
            }
            break;

        default:
            Log.Error("❌ Unknown configuration key: {Key}", key);
            Log.Information("ℹ️  Valid keys: webhook, interval");
            break;
    }
}

static async Task RunOnce()
{
    Log.Information("🔄 Running single report...");

    try
    {
        var config = ConfigurationManager.LoadConfig();

        Log.Information("📡 Collecting device information...");
        var deviceInfo = await DeviceInfoCollector.CollectAsync();

        Log.Information("📤 Sending report to: {Webhook}", config.WebhookUrl);
        var json = JsonSerializer.Serialize(deviceInfo);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        var response = await client.PostAsync(config.WebhookUrl, content);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        Log.Information("✅ Report sent successfully");
        Log.Information("📥 Response: {Response}", responseBody);
    }
    catch (HttpRequestException ex)
    {
        Log.Error(ex, "❌ HTTP error sending report");
        Log.Information("💡 Tip: Verify webhook URL is correct and server is running");
        Log.Information("💡 Use 'Tracker.Cli.exe config' to check current settings");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "❌ Failed to send report");
    }
}

static async Task TestConfig()
{
    Log.Information("🧪 Testing configuration...");

    try
    {
        var config = ConfigurationManager.LoadConfig();
        Log.Information("📋 Configuration loaded:");
        Log.Information("  Webhook: {Webhook}", config.WebhookUrl);
        Log.Information("  Interval: {Interval}s", config.ReportIntervalSeconds);
        Log.Information("  Retry Count: {RetryCount}", config.RetryCount);

        // Test webhook connectivity
        Log.Information("🌐 Testing webhook connectivity...");
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        // Send a test ping
        var testData = new
        {
            test = true,
            timestamp = DateTime.UtcNow,
            message = "Configuration test from LaptopTracker"
        };

        var json = JsonSerializer.Serialize(testData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(config.WebhookUrl, content);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        Log.Information("✅ Test completed successfully");
        Log.Information("📥 Server response: {Response}", responseBody);
    }
    catch (HttpRequestException ex)
    {
        Log.Error(ex, "❌ HTTP error during test");
        Log.Information("💡 Tip: Make sure the server is running and accessible");
        Log.Information("💡 Use 'Tracker.Cli.exe config' to check webhook URL");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "❌ Test failed");
    }
}

static void ShowVersion()
{
    var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
    Console.WriteLine(@$"
Laptop Tracker v{version}
⚠️ For educational and ethical use only

Features:
  ✅ Windows Service
  ✅ Device Monitoring
  ✅ Geolocation
  ✅ Configurable Reporting
  ✅ CLI Management

License: MIT
");
}

// ---------------- CONFIGURATION ----------------
public static class ConfigurationManager
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Tracker",
        "config.json");

    public static TrackerConfig LoadConfig()
    {
        if (!File.Exists(ConfigPath))
            return new TrackerConfig();

        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<TrackerConfig>(json) ?? new TrackerConfig();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Failed to load configuration, using defaults");
            return new TrackerConfig();
        }
    }

    public static void SaveConfig(TrackerConfig config)
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir!);

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Failed to save configuration");
        }
    }
}

public class TrackerConfig
{
    public string WebhookUrl { get; set; } = "http://localhost:5001/api/report";
    public int ReportIntervalSeconds { get; set; } = 300;
    public int RetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 5;
    public bool EnableGeoLocation { get; set; } = true;
    public bool EnableBatteryMonitoring { get; set; } = true;
    public bool EnableDiskMonitoring { get; set; } = true;
}