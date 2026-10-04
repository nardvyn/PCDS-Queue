using System.Net.Http;
using System.Text.Json;
using PCDSQueue.Admin.Models;

namespace PCDSQueue.Admin.Services;

public sealed class ReportService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ReportResponse> GetReportAsync(
        DateTime dateFrom,
        DateTime dateTo,
        int? departmentId)
    {
        var path = $"api/admin/reports?from={dateFrom:yyyy-MM-dd}&to={dateTo:yyyy-MM-dd}";
        if (departmentId is not null)
            path += $"&department_id={departmentId.Value}";

        using var response = await ApiService.Client.GetAsync(path);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var message = "Unable to generate the report.";
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

        return JsonSerializer.Deserialize<ReportResponse>(json, SerializerOptions)
            ?? throw new Exception("Invalid report response from the server.");
    }
}