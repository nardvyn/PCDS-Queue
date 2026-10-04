using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using PCDSQueue.Staff.Models;

namespace PCDSQueue.Staff.Services;

public sealed class StaffService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<WindowsResponse> GetWindowsAsync() =>
        SendAsync<WindowsResponse>(() => ApiService.Client.GetAsync("api/staff/windows"));

    public Task<CurrentShiftResponse> GetCurrentShiftAsync() =>
        SendAsync<CurrentShiftResponse>(() => ApiService.Client.GetAsync("api/staff/current-shift"));

    public Task<ShiftResponse> StartShiftAsync(int windowId) =>
        SendAsync<ShiftResponse>(() =>
            ApiService.Client.PostAsJsonAsync("api/staff/start-shift", new { window_id = windowId }));

    public Task<ShiftResponse> EndShiftAsync() =>
        SendAsync<ShiftResponse>(() => ApiService.Client.PostAsync("api/staff/end-shift", null));

    private static async Task<T> SendAsync<T>(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            using var response = await send();
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(ReadMessage(json) ?? "The server could not complete the request.");
            }

            var result = JsonSerializer.Deserialize<T>(json, SerializerOptions);
            return result ?? throw new Exception("Invalid response from the server.");
        }
        catch (HttpRequestException)
        {
            throw new Exception("Cannot connect to the PCDS Queue server.");
        }
        catch (TaskCanceledException)
        {
            throw new Exception("The server is taking too long to respond.");
        }
    }

    private static string? ReadMessage(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("message", out var message)
                ? message.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
