using System.Net.Http;
using System.Text.Json;
using PCDSQueue.Admin.Models;

namespace PCDSQueue.Admin.Services;

public sealed class LiveQueueService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<LiveQueueResponse> GetSnapshotAsync(int? departmentId)
    {
        var path = departmentId is null
            ? "api/admin/live-queue"
            : $"api/admin/live-queue?department_id={departmentId.Value}";
        using var response = await ApiService.Client.GetAsync(path);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var message = "Unable to load the live queue.";
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

        return JsonSerializer.Deserialize<LiveQueueResponse>(json, SerializerOptions)
            ?? throw new Exception("Invalid live-queue response from the server.");
    }
}