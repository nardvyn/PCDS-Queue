using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using PCDSQueue.Staff.Models;

namespace PCDSQueue.Staff.Services;

public sealed class QueueService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<QueueStatusResponse> GetStatusAsync(int departmentId) =>
        SendAsync<QueueStatusResponse>(() =>
            ApiService.Client.GetAsync($"api/queue/status/{departmentId}"));

    public Task<QueueDashboardResponse> GetDashboardAsync() =>
        SendAsync<QueueDashboardResponse>(() =>
            ApiService.Client.GetAsync("api/queue/dashboard"));

    public Task<QueueHistoryResponse> GetHistoryAsync() =>
        SendAsync<QueueHistoryResponse>(() =>
            ApiService.Client.GetAsync("api/queue/history"));

    public Task NextAsync() =>
        SendAsync<object>(() =>
            ApiService.Client.PostAsync("api/queue/next", null));

    public Task RecallAsync(long queueId) => PostActionAsync(queueId, "recall");

    public Task CompleteAsync(long queueId) => PostActionAsync(queueId, "complete");

    public Task NoShowAsync(long queueId) => PostActionAsync(queueId, "no-show");

    private Task PostActionAsync(long queueId, string action) =>
        SendAsync<object>(() =>
            ApiService.Client.PostAsync($"api/queue/{queueId}/{action}", null));

    private static async Task<T> SendAsync<T>(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            using var response = await send();
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(ReadMessage(json) ?? "The queue request could not be completed.");
            }

            return JsonSerializer.Deserialize<T>(json, SerializerOptions)
                ?? throw new Exception("Invalid response from the server.");
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
