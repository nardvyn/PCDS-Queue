using System.Net.Http;
using System.Text.Json;
using PCDSQueue.Kiosk.Models;

namespace PCDSQueue.Kiosk.Services;

public sealed class DepartmentService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<DepartmentModel>> GetDepartmentsAsync()
    {
        try
        {
            using var response = await ApiService.Client.GetAsync("api/kiosk/departments");
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception("Unable to load departments from the server.");

            var result = JsonSerializer.Deserialize<DepartmentResponse>(json, SerializerOptions);
            return result?.Departments ?? [];
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
}