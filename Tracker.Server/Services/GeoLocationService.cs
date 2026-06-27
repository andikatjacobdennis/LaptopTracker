using Serilog;
using System.Text.Json;

namespace Tracker.Server.Services;

public static class GeoLocationService
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    private static readonly List<GeoProvider> _providers = new()
    {
        new GeoProvider("https://ipapi.co/{ip}/json/", ParseIpApi),
        new GeoProvider("https://ip-api.com/json/{ip}?fields=status,message,city,lat,lon", ParseIpApiCom),
        new GeoProvider("https://freeipapi.com/api/json/{ip}", ParseFreeIpApi)
    };

    public static async Task<(double? lat, double? lon, string? city)> GetLocationAsync(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip) || IsPrivateIp(ip))
        {
            Log.Information("Skipping geo lookup for private/empty IP: {Ip}", ip);
            return (null, null, null);
        }

        Log.Information("📍 Performing geo lookup for IP: {Ip}", ip);

        foreach (var provider in _providers)
        {
            try
            {
                var url = provider.Url.Replace("{ip}", ip);
                Log.Debug("Trying geo provider: {Url}", url);

                var json = await _httpClient.GetStringAsync(url);
                Log.Debug("Geo provider response received (length: {Length})", json.Length);

                var (lat, lon, city) = provider.Parser(json);

                if (lat.HasValue && lon.HasValue)
                {
                    Log.Information("✅ Geo lookup successful via {Provider} - City: {City}, Lat: {Lat:F4}, Lon: {Lon:F4}",
                        provider.Name, city ?? "unknown", lat.Value, lon.Value);
                    return (lat, lon, city);
                }

                Log.Warning("⚠️ Geo provider {Provider} returned incomplete data", provider.Name);
            }
            catch (HttpRequestException ex)
            {
                Log.Warning("⚠️ Network error with provider {Provider}: {Message}", provider.Name, ex.Message);
            }
            catch (Exception ex)
            {
                Log.Warning("⚠️ Error with provider {Provider}: {Message}", provider.Name, ex.Message);
            }
        }

        Log.Warning("❌ All geo providers failed for IP: {Ip}", ip);
        return (null, null, null);
    }

    private static bool IsPrivateIp(string ip)
    {
        // Check for common private/local IP patterns
        return ip.StartsWith("10.") ||
               ip.StartsWith("192.168.") ||
               ip.StartsWith("172.16.") ||
               ip.StartsWith("172.17.") ||
               ip.StartsWith("172.18.") ||
               ip.StartsWith("172.19.") ||
               ip.StartsWith("172.20.") ||
               ip.StartsWith("172.21.") ||
               ip.StartsWith("172.22.") ||
               ip.StartsWith("172.23.") ||
               ip.StartsWith("172.24.") ||
               ip.StartsWith("172.25.") ||
               ip.StartsWith("172.26.") ||
               ip.StartsWith("172.27.") ||
               ip.StartsWith("172.28.") ||
               ip.StartsWith("172.29.") ||
               ip.StartsWith("172.30.") ||
               ip.StartsWith("172.31.") ||
               ip == "127.0.0.1" ||
               ip == "::1" ||
               ip == "localhost";
    }

    // Parser for ipapi.co
    private static (double? lat, double? lon, string? city) ParseIpApi(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Check for error response
        if (root.TryGetProperty("error", out _))
        {
            return (null, null, null);
        }

        double? lat = root.TryGetProperty("latitude", out var la) ? la.GetDouble() : null;
        double? lon = root.TryGetProperty("longitude", out var lo) ? lo.GetDouble() : null;
        string? city = root.TryGetProperty("city", out var c) ? c.GetString() : null;

        return (lat, lon, city);
    }

    // Parser for ip-api.com
    private static (double? lat, double? lon, string? city) ParseIpApiCom(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Check for error response
        if (root.TryGetProperty("status", out var status) && status.GetString() == "fail")
        {
            return (null, null, null);
        }

        double? lat = root.TryGetProperty("lat", out var la) ? la.GetDouble() : null;
        double? lon = root.TryGetProperty("lon", out var lo) ? lo.GetDouble() : null;
        string? city = root.TryGetProperty("city", out var c) ? c.GetString() : null;

        return (lat, lon, city);
    }

    // Parser for freeipapi.com
    private static (double? lat, double? lon, string? city) ParseFreeIpApi(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        double? lat = root.TryGetProperty("latitude", out var la) ? la.GetDouble() : null;
        double? lon = root.TryGetProperty("longitude", out var lo) ? lo.GetDouble() : null;
        string? city = root.TryGetProperty("cityName", out var c) ? c.GetString() : null;

        return (lat, lon, city);
    }

    private record GeoProvider(string Url, Func<string, (double? lat, double? lon, string? city)> Parser)
    {
        public string Name => Url.Split('/')[2].Replace("www.", "").Split('.')[0];
    }
}