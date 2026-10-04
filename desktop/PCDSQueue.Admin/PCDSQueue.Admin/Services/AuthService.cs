using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using PCDSQueue.Admin.Models;

namespace PCDSQueue.Admin.Services;

public sealed class AuthService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<LoginResponse> LoginAsync(string username, string password)
    {
        try
        {
            using var response = await ApiService.Client.PostAsJsonAsync(
                "api/auth/login",
                new LoginRequest { Username = username.Trim(), Password = password });

            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(ReadServerMessage(json) ?? "Invalid username or password.");
            }

            var result = JsonSerializer.Deserialize<LoginResponse>(json, SerializerOptions);
            if (result is null || string.IsNullOrWhiteSpace(result.AccessToken))
            {
                throw new Exception("Invalid response from the server.");
            }

            return result;
        }
        catch (HttpRequestException)
        {
            throw new Exception("Cannot connect to the PCDS Queue server. Check that the Flask API is running.");
        }
        catch (TaskCanceledException)
        {
            throw new Exception("The server is taking too long to respond.");
        }
    }

    private static string? ReadServerMessage(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("message", out var message)
                ? message.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}