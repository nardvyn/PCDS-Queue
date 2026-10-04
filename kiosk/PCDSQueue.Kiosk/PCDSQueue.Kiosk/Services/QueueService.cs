using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using PCDSQueue.Kiosk.Models;

namespace PCDSQueue.Kiosk.Services;

public sealed class QueueService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<QueueTicketModel> GenerateQueueAsync(int departmentId)
    {
        try
        {
            using var response = await ApiService.Client.PostAsJsonAsync(
                "api/queue/generate",
                new GenerateQueueRequest { DepartmentId = departmentId, Source = "KIOSK" });
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(ReadServerMessage(json));

            var result = JsonSerializer.Deserialize<GenerateQueueResponse>(json, SerializerOptions);
            return result?.Queue ?? throw new Exception("Invalid queue response from the server.");
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

    private static string ReadServerMessage(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("message", out var message))
                return message.GetString() ?? "Unable to generate queue number.";
        }
        catch (JsonException)
        {
        }

        return "Unable to generate queue number.";
    }
}