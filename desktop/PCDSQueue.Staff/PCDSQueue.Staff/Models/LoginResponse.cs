using System.Text.Json.Serialization;

namespace PCDSQueue.Staff.Models;

public sealed class LoginResponse
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("user")]
    public UserModel? User { get; init; }
}

public sealed class UserModel
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
