using System.Text.Json;
using PCDSQueue.Admin.Models;

namespace PCDSQueue.Admin.Services;

public sealed class AdminDashboardService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<AdminDashboardResponse> GetDashboardAsync()
    {
        using var response = await ApiService.Client.GetAsync("api/admin/dashboard");
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var message = "Unable to load the admin dashboard.";
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

        return JsonSerializer.Deserialize<AdminDashboardResponse>(json, SerializerOptions)
            ?? throw new Exception("Invalid dashboard response from the server.");
    }
}