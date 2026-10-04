using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using PCDSQueue.Admin.Models;

namespace PCDSQueue.Admin.Services;

public sealed class DepartmentService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<DepartmentModel>> GetDepartmentsAsync()
    {
        using var response = await ApiService.Client.GetAsync("api/admin/departments");
        var json = await response.Content.ReadAsStringAsync();
        EnsureSuccess(response, json);

        var result = JsonSerializer.Deserialize<DepartmentListResponse>(json, SerializerOptions);
        return result?.Departments ?? throw new Exception("Invalid departments response from the server.");
    }

    public Task CreateDepartmentAsync(DepartmentSaveRequest request) =>
        SendAsync(() => ApiService.Client.PostAsJsonAsync("api/admin/departments", request));

    public Task UpdateDepartmentAsync(int departmentId, DepartmentSaveRequest request) =>
        SendAsync(() => ApiService.Client.PutAsJsonAsync($"api/admin/departments/{departmentId}", request));

    public Task SetDepartmentStatusAsync(int departmentId, bool isActive) =>
        SendAsync(() => ApiService.Client.PatchAsJsonAsync(
            $"api/admin/departments/{departmentId}/status",
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

        var message = "The server could not complete the department request.";
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