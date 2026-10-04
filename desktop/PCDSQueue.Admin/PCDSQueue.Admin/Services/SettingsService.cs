using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using PCDSQueue.Admin.Models;

namespace PCDSQueue.Admin.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<SystemSettingsResponse> GetSettingsAsync()
    {
        using var response = await ApiService.Client.GetAsync("api/admin/settings");
        var json = await response.Content.ReadAsStringAsync();
        EnsureSuccess(response, json);

        return JsonSerializer.Deserialize<SystemSettingsResponse>(json, SerializerOptions)
            ?? throw new Exception("Invalid settings response from the server.");
    }

    public async Task<SystemSettingsResponse> SaveSettingsAsync(SystemSettingsModel settings)
    {
        using var response = await ApiService.Client.PutAsJsonAsync(
            "api/admin/settings",
            new { settings });
        var json = await response.Content.ReadAsStringAsync();
        EnsureSuccess(response, json);

        return JsonSerializer.Deserialize<SystemSettingsResponse>(json, SerializerOptions)
            ?? throw new Exception("Invalid settings response from the server.");
    }

    public async Task<SystemHealthModel> GetHealthAsync()
    {
        using var response = await ApiService.Client.GetAsync("api/health");
        var json = await response.Content.ReadAsStringAsync();
        var health = JsonSerializer.Deserialize<SystemHealthModel>(json, SerializerOptions)
            ?? new SystemHealthModel();
        return new SystemHealthModel
        {
            ApiServer = "Online",
            Status = health.Status,
            Database = health.Database
        };
    }

    private static void EnsureSuccess(HttpResponseMessage response, string json)
    {
        if (response.IsSuccessStatusCode)
            return;

        var message = "The server could not complete the settings request.";
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