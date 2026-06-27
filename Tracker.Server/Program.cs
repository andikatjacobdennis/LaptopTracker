using Serilog;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// ---------------- START APP ----------------
var builder = WebApplication.CreateBuilder(args);

// ---------------- SERILOG ----------------
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/server-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80);
});

var app = builder.Build();

// ---------------- GEO SERVICE (INLINE) ----------------
static async Task<(double? lat, double? lon, string? city)> GetGeoAsync(string ip)
{
    try
    {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        var json = await client.GetStringAsync($"https://ipapi.co/{ip}/json/");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        double? lat = root.TryGetProperty("latitude", out var la) ? la.GetDouble() : null;
        double? lon = root.TryGetProperty("longitude", out var lo) ? lo.GetDouble() : null;
        string? city = root.TryGetProperty("city", out var c) ? c.GetString() : null;

        return (lat, lon, city);
    }
    catch
    {
        return (null, null, null);
    }
}

// ---------------- ROUTES ----------------

// Health check
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow }));

// Report endpoint
app.MapPost("/api/report", async (HttpContext ctx) =>
{
    try
    {
        // Read raw request body
        string rawBody;
        using (var reader = new StreamReader(ctx.Request.Body))
        {
            rawBody = await reader.ReadToEndAsync();
        }

        Log.Information("Raw request body received (length: {Length})", rawBody.Length);

        // Parse the JSON to handle both wrapped and unwrapped formats
        DeviceReportDto? dto = null;

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            // Check if the data is wrapped in a "result" property
            if (root.TryGetProperty("result", out var resultElement) && resultElement.ValueKind == JsonValueKind.Object)
            {
                Log.Information("Detected wrapped format - extracting from 'result' property");

                // Extract from the wrapper
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                };

                dto = JsonSerializer.Deserialize<DeviceReportDto>(resultElement.GetRawText(), options);

                // Also check if there's an ID in the wrapper
                if (root.TryGetProperty("id", out var idElement))
                {
                    Log.Information("Request ID: {RequestId}", idElement.GetInt64());
                }
            }
            else
            {
                // Direct format - deserialize directly
                Log.Information("Detected direct format");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                };

                dto = JsonSerializer.Deserialize<DeviceReportDto>(rawBody, options);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to parse JSON body");
            return Results.BadRequest(new
            {
                error = "Invalid JSON format",
                details = ex.Message
            });
        }

        if (dto is null)
        {
            return Results.BadRequest(new { error = "Invalid payload - deserialization returned null" });
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(dto.DeviceId))
        {
            Log.Warning("DeviceId is missing or empty");
            return Results.BadRequest(new { error = "DeviceId is required" });
        }

        Log.Information("✅ Successfully parsed device report for: {DeviceId}", dto.DeviceId);

        var clientIp = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        Log.Information("Client IP: {ClientIp}", clientIp);

        // 🔥 Geo lookup (only for real IPs, not localhost)
        double? lat = null;
        double? lon = null;
        string? city = null;

        var isLocalhost = clientIp is "::1" or "127.0.0.1" or "localhost";
        if (!isLocalhost && clientIp != "unknown")
        {
            Log.Information("Performing geo lookup for IP: {ClientIp}", clientIp);
            (lat, lon, city) = await GetGeoAsync(clientIp);

            if (lat.HasValue && lon.HasValue)
            {
                Log.Information("📍 Geo lookup successful - Lat: {Lat:F4}, Lon: {Lon:F4}, City: {City}", lat, lon, city);
            }
            else
            {
                Log.Warning("⚠️ Geo lookup failed or returned no data for IP: {ClientIp}", clientIp);
            }
        }
        else
        {
            Log.Information("Skipping geo lookup for localhost/unknown IP");
        }

        // Use client-provided location as fallback if geo lookup failed
        var finalLat = lat ?? dto.Latitude;
        var finalLon = lon ?? dto.Longitude;
        var finalCity = city ?? "unknown";

        Log.Information(
            "📱 Device report | DeviceId: {DeviceId} | User: {User} | ClientIP: {ClientIP} | " +
            "PublicIP: {PublicIP} | LocalIP: {LocalIP} | MAC: {MAC} | " +
            "City: {City} | Lat: {Lat:F4} | Lon: {Lon:F4} | OS: {OS} | Timestamp: {Timestamp}",
            dto.DeviceId,
            dto.Username ?? "unknown",
            clientIp,
            dto.PublicIp ?? "unknown",
            dto.LocalIp ?? "unknown",
            dto.MacAddress ?? "unknown",
            finalCity,
            finalLat,
            finalLon,
            dto.OperatingSystem ?? "unknown",
            dto.Timestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? "unknown"
        );

        return Results.Ok(new
        {
            status = "success",
            message = "Device report logged successfully",
            timestamp = DateTime.UtcNow,
            deviceId = dto.DeviceId,
            city = finalCity,
            latitude = finalLat,
            longitude = finalLon,
            geo_source = lat.HasValue ? "server" : "client"
        });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Unhandled exception processing request");
        return Results.StatusCode(500);
    }
});

// ---------------- START ----------------
Log.Information("🚀 Tracker Server started on port 80");
Log.Information("📡 Listening for device reports at /api/report");

app.Run();

// ---------------- DTO ----------------
public record DeviceReportDto
{
    [JsonPropertyName("deviceId")]
    public string? DeviceId { get; init; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("operatingSystem")]
    public string? OperatingSystem { get; init; }

    [JsonPropertyName("publicIp")]
    public string? PublicIp { get; init; }

    [JsonPropertyName("localIp")]
    public string? LocalIp { get; init; }

    [JsonPropertyName("macAddress")]
    public string? MacAddress { get; init; }

    [JsonPropertyName("wifiSsid")]
    public string? WifiSsid { get; init; }

    [JsonPropertyName("latitude")]
    public double? Latitude { get; init; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; init; }

    [JsonPropertyName("raw")]
    public Dictionary<string, object>? Raw { get; init; }
}