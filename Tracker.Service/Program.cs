using Serilog;
using System.Text;
using System.Text.Json;

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
    Log.Information("🚀 Starting LaptopTracker Service");
    Log.Warning("⚠️ This software is for educational and ethical use only");
    Log.Warning("⚠️ Only use on devices you own or have permission to track");
    Log.Warning("⚠️ The author assumes NO responsibility for misuse");

    var builder = Host.CreateApplicationBuilder(args);

    // Add Windows Service
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "LaptopTracker";
    });

    // Add HTTP client
    builder.Services.AddHttpClient();

    // Add hosted service
    builder.Services.AddHostedService<Worker>();

    // Load configuration from file
    var config = ConfigurationManager.LoadConfig();
    builder.Services.AddSingleton(config);

    // Add Serilog
    builder.Services.AddSerilog();

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "💀 Service crashed unexpectedly");
}
finally
{
    Log.CloseAndFlush();
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
        {
            Log.Warning("⚠️ Configuration file not found at: {Path}", ConfigPath);
            Log.Information("📋 Using default configuration");
            return new TrackerConfig();
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<TrackerConfig>(json) ?? new TrackerConfig();
            Log.Information("✅ Configuration loaded from: {Path}", ConfigPath);
            Log.Information("📋 Webhook URL: {Webhook}", config.WebhookUrl);
            Log.Information("📋 Interval: {Interval}s", config.ReportIntervalSeconds);
            Log.Information("📋 Retry Count: {RetryCount}", config.RetryCount);
            return config;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Failed to load configuration, using defaults");
            return new TrackerConfig();
        }
    }
}

public class TrackerConfig
{
    public string WebhookUrl { get; set; } = "http://localhost:5001/api/report";
    public int ReportIntervalSeconds { get; set; } = 300; // 5 minutes default
    public int RetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 5;
    public bool EnableGeoLocation { get; set; } = true;
    public bool EnableBatteryMonitoring { get; set; } = true;
    public bool EnableDiskMonitoring { get; set; } = true;
}