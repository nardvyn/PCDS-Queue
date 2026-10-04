using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using PCDSQueue.Admin.Models;

namespace PCDSQueue.Admin.Services;

public sealed class StaffAccountService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<StaffAccountModel>> GetStaffAsync()
    {
        using var response = await ApiService.Client.GetAsync("api/admin/staff");
        var json = await response.Content.ReadAsStringAsync();
        EnsureSuccess(response, json);

        var result = JsonSerializer.Deserialize<StaffAccountListResponse>(json, SerializerOptions);
        return result?.Staff ?? throw new Exception("Invalid staff response from the server.");
    }

    public Task CreateStaffAsync(StaffAccountSaveRequest request) =>
        SendAsync(() => ApiService.Client.PostAsJsonAsync("api/admin/staff", request));

    public Task UpdateStaffAsync(int staffId, StaffAccountSaveRequest request) =>
        SendAsync(() => ApiService.Client.PutAsJsonAsync($"api/admin/staff/{staffId}", request));

    public Task SetStaffStatusAsync(int staffId, string accountStatus) =>
        SendAsync(() => ApiService.Client.PatchAsJsonAsync(
            $"api/admin/staff/{staffId}/status",
            new { account_status = accountStatus }));

    public Task ResetPasswordAsync(int staffId, string password) =>
        SendAsync(() => ApiService.Client.PostAsJsonAsync(
            $"api/admin/staff/{staffId}/reset-password",
            new { password }));

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

        var message = "The server could not complete the staff-account request.";
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