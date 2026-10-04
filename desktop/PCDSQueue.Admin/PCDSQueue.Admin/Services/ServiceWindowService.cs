using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using PCDSQueue.Admin.Models;

namespace PCDSQueue.Admin.Services;

public sealed class ServiceWindowService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<ServiceWindowModel>> GetWindowsAsync(int? departmentId)
    {
        var path = departmentId is null
            ? "api/admin/windows"
            : $"api/admin/windows?department_id={departmentId.Value}";
        using var response = await ApiService.Client.GetAsync(path);
        var json = await response.Content.ReadAsStringAsync();
        EnsureSuccess(response, json);

        var result = JsonSerializer.Deserialize<ServiceWindowListResponse>(json, SerializerOptions);
        return result?.Windows ?? throw new Exception("Invalid service-windows response from the server.");
    }

        public Task CreateWindowAsync(int departmentId, string? windowName = null) =>
        SendAsync(() => ApiService.Client.PostAsJsonAsync(
            "api/admin/windows",
                new ServiceWindowCreateRequest { DepartmentId = departmentId, WindowName = windowName }));

    public Task UpdateWindowAsync(int windowId, string windowName) =>
        SendAsync(() => ApiService.Client.PutAsJsonAsync(
            $"api/admin/windows/{windowId}",
            new ServiceWindowUpdateRequest { WindowName = windowName }));

    public Task SetWindowStatusAsync(int windowId, bool isActive) =>
        SendAsync(() => ApiService.Client.PatchAsJsonAsync(
            $"api/admin/windows/{windowId}/status",
            new { is_active = isActive }));

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

        var message = "The server could not complete the service-window request.";
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