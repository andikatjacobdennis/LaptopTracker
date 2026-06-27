using Serilog;
using System.Text;
using System.Text.Json;
using Tracker.Shared.Utilities;

public class Worker : BackgroundService
{
    private readonly HttpClient _httpClient;
    private readonly TrackerConfig _config;

    public Worker(HttpClient httpClient, TrackerConfig config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("🔄 Worker started");
        Log.Information("📋 Configuration:");
        Log.Information("  Webhook URL: {Webhook}", _config.WebhookUrl);
        Log.Information("  Interval: {Interval} seconds", _config.ReportIntervalSeconds);
        Log.Information("  Retry Count: {RetryCount}", _config.RetryCount);

        // Send initial report immediately
        await SendReportWithRetry(stoppingToken);

        // Then send reports at the configured interval
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_config.ReportIntervalSeconds), stoppingToken);
                await SendReportWithRetry(stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Error in worker loop");
                // Wait a bit before retrying on error
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        Log.Information("🛑 Worker stopped");
    }

    private async Task SendReportWithRetry(CancellationToken stoppingToken)
    {
        var retryCount = 0;
        var maxRetries = _config.RetryCount;

        while (retryCount <= maxRetries && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                Log.Information("📡 Collecting device information...");
                var deviceInfo = await DeviceInfoCollector.CollectAsync();

                Log.Information("📤 Sending report to: {Webhook}", _config.WebhookUrl);
                var json = JsonSerializer.Serialize(deviceInfo);
                Log.Debug("📦 Payload size: {Size} bytes", json.Length);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_config.WebhookUrl, content, stoppingToken);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync(stoppingToken);
                Log.Information("✅ Report sent successfully");
                Log.Information("📥 Response: {Response}", responseBody);

                // Success - exit retry loop
                return;
            }
            catch (HttpRequestException ex)
            {
                retryCount++;
                if (retryCount <= maxRetries)
                {
                    Log.Warning("⚠️ HTTP error (Attempt {RetryCount}/{MaxRetries}): {Message}",
                        retryCount, maxRetries, ex.Message);
                    Log.Information("💡 Retrying in {Delay} seconds...", _config.RetryDelaySeconds);

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(_config.RetryDelaySeconds), stoppingToken);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
                else
                {
                    Log.Error(ex, "❌ Failed to send report after {MaxRetries} retries", maxRetries);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Unexpected error sending report");
                break;
            }
        }
    }
}