using System.Net.Http;
using System.Text.Json;
using PCDSQueue.Admin.Models;

namespace PCDSQueue.Admin.Services;

public sealed class QueueHistoryService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<AdminQueueHistoryItem>> SearchAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        int? departmentId,
        string? status,
        string? source)
    {
        var query = new List<string>();
        if (dateFrom is not null)
            query.Add($"date_from={Uri.EscapeDataString(dateFrom.Value.ToString("yyyy-MM-dd"))}");
        if (dateTo is not null)
            query.Add($"date_to={Uri.EscapeDataString(dateTo.Value.ToString("yyyy-MM-dd"))}");
        if (departmentId is not null)
            query.Add($"department_id={departmentId.Value}");
        if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
            query.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(source) && source != "ALL")
            query.Add($"source={Uri.EscapeDataString(source)}");

        var path = "api/admin/queue-history" + (query.Count > 0 ? $"?{string.Join("&", query)}" : string.Empty);
        using var response = await ApiService.Client.GetAsync(path);
        var json = await response.Content.ReadAsStringAsync();
        EnsureSuccess(response, json);

        var result = JsonSerializer.Deserialize<AdminQueueHistoryResponse>(json, SerializerOptions);
        return result?.History ?? throw new Exception("Invalid queue-history response from the server.");
    }

    public async Task<AdminQueueHistoryDetailResponse> GetDetailsAsync(long queueId)
    {
        using var response = await ApiService.Client.GetAsync($"api/admin/queue-history/{queueId}");
        var json = await response.Content.ReadAsStringAsync();
        EnsureSuccess(response, json);

        return JsonSerializer.Deserialize<AdminQueueHistoryDetailResponse>(json, SerializerOptions)
            ?? throw new Exception("Invalid queue transaction details from the server.");
    }

    private static void EnsureSuccess(HttpResponseMessage response, string json)
    {
        if (response.IsSuccessStatusCode)
            return;

        var message = "The server could not complete the queue-history request.";
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
}