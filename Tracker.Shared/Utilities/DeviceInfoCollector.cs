using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;

namespace Tracker.Shared.Utilities;

public static class DeviceInfoCollector
{
    public static async Task<DeviceInfo> CollectAsync()
    {
        var publicIpTask = GetPublicIpAsync();
        var localIp = GetLocalIp();
        var mac = GetMac();

        return new DeviceInfo
        {
            DeviceId = Environment.MachineName,
            Hostname = Environment.MachineName,
            Username = Environment.UserName,
            PublicIp = await publicIpTask,
            LocalIp = localIp,
            MacAddress = mac,
            WifiSsid = string.Empty,
            Latitude = null,
            Longitude = null,
            Timestamp = DateTime.UtcNow,
            Raw = null
        };
    }

    private static async Task<string> GetPublicIpAsync()
    {
        var providers = new[]
        {
            new IpProvider("https://api.ipify.org?format=json", true),
            new IpProvider("https://checkip.amazonaws.com", false),
            new IpProvider("https://icanhazip.com", false)
        };

        using var client = new HttpClient();

        foreach (var provider in providers)
        {
            try
            {
                var response = (await client.GetStringAsync(provider.Url)).Trim();
                return provider.IsJsonFormat
                    ? ParseIpFromJson(response)
                    : response;
            }
            catch
            {
                // Continue to next provider on failure
            }
        }

        return "unknown";
    }

    private static string ParseIpFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("ip").GetString() ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    private static string GetLocalIp()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ip = host.AddressList.FirstOrDefault(IsValidLocalIp);
            return ip?.ToString() ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    private static bool IsValidLocalIp(IPAddress address) =>
        address.AddressFamily == AddressFamily.InterNetwork &&
        !IPAddress.IsLoopback(address);

    private static string GetMac()
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(IsValidNetworkInterface);
            return nic?.GetPhysicalAddress().ToString() ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    private static bool IsValidNetworkInterface(NetworkInterface nic) =>
        nic.OperationalStatus == OperationalStatus.Up &&
        nic.NetworkInterfaceType != NetworkInterfaceType.Loopback;

    private record IpProvider(string Url, bool IsJsonFormat);
}

public record DeviceInfo
{
    public string DeviceId { get; init; } = string.Empty;
    public string Hostname { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string PublicIp { get; init; } = string.Empty;
    public string LocalIp { get; init; } = string.Empty;
    public string MacAddress { get; init; } = string.Empty;
    public string WifiSsid { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public DateTime Timestamp { get; init; }
    public object? Raw { get; init; }
}