using System.Text.Json.Serialization;

namespace PCDSQueue.Admin.Models;

public sealed class LoginRequest
{
    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; init; } = string.Empty;
}

public sealed class LoginResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("user")]
    public AdminUserModel? User { get; init; }
}

public sealed class AdminUserModel
{
    [JsonPropertyName("staff_id")]
    public int StaffId { get; init; }

    [JsonPropertyName("full_name")]
    public string FullName { get; init; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;
}