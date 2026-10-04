using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using PCDSQueue.Admin.Models;

namespace PCDSQueue.Admin.Services;

public sealed class QrManagementService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<QrSessionModel>> GetSessionsAsync(int departmentId)
    {
        using var response = await ApiService.Client.GetAsync($"api/admin/qr?department_id={departmentId}");
        var json = await response.Content.ReadAsStringAsync();
        EnsureSuccess(response, json);

        var result = JsonSerializer.Deserialize<QrSessionListResponse>(json, SerializerOptions);
        return result?.Sessions ?? throw new Exception("Invalid QR session response from the server.");
    }

    public async Task<GeneratedQrSession> GenerateAsync(int departmentId)
    {
        using var response = await ApiService.Client.PostAsJsonAsync(
            "api/admin/qr/generate",
            new { department_id = departmentId });
        var json = await response.Content.ReadAsStringAsync();
        EnsureSuccess(response, json);

        var result = JsonSerializer.Deserialize<QrGenerateResponse>(json, SerializerOptions);
        return result?.Session ?? throw new Exception("Invalid QR generation response from the server.");
    }

    public Task DeactivateAsync(long sessionId) =>
        SendAsync(() => ApiService.Client.PostAsync($"api/admin/qr/{sessionId}/deactivate", null));

    private static async Task SendAsync(Func<Task<HttpResponseMessage>> send)
    {
        using var response = await send();
        var json = await response.Content.ReadAsStringAsync();
        EnsureSuccess(response, json);
    }

    private static void EnsureSuccess(HttpResponseMessage response, string json)
    {
        if (response.IsSuccessStatusCode)
            return;

        var message = "The server could not complete the QR request.";
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