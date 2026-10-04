using System.Net.Http;
using System.Text.Json;
using PCDSQueue.Kiosk.Models;

namespace PCDSQueue.Kiosk.Services;

public sealed class DepartmentQueueService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<KioskQrSessionResponse> CreateQrSessionAsync(int departmentId) =>
        SendAsync<KioskQrSessionResponse>(
            $"api/kiosk/departments/{departmentId}/qr-session",
            HttpMethod.Post);

    public Task<DepartmentQueueStatusResponse> GetStatusAsync(int departmentId) =>
        SendAsync<DepartmentQueueStatusResponse>(
            $"api/queue/status/{departmentId}",
            HttpMethod.Get);

    public Task<KioskQrSessionStatusResponse> GetQrSessionStatusAsync(string token) =>
        SendAsync<KioskQrSessionStatusResponse>(
            $"api/kiosk/qr-session/{Uri.EscapeDataString(token)}/status",
            HttpMethod.Get);

    private static async Task<T> SendAsync<T>(string path, HttpMethod method)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            using var response = await ApiService.Client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var message = "The server could not load this department.";
                try
                {
                    using var document = JsonDocument.Parse(json);
                    if (document.RootElement.TryGetProperty("message", out var serverMessage))
                        message = serverMessage.GetString() ?? message;
                }
                catch (JsonException)
                {
                }

                throw new Exception(message);
            }

            return JsonSerializer.Deserialize<T>(json, SerializerOptions)
                ?? throw new Exception("Invalid response from the server.");
        }
        catch (HttpRequestException)
        {
            throw new Exception("Cannot connect to the PCDS Queue server. Check that Flask is running.");
        }
        catch (TaskCanceledException)
        {
            throw new Exception("The PCDS Queue server is taking too long to respond.");
        }
    }
}