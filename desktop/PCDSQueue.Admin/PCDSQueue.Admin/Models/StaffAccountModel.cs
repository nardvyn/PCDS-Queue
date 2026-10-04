using System.Text.Json.Serialization;

namespace PCDSQueue.Admin.Models;

public sealed class StaffAccountListResponse
{
    [JsonPropertyName("staff")]
    public List<StaffAccountModel> Staff { get; init; } = [];
}

public sealed class StaffAccountModel
{
    [JsonPropertyName("staff_id")]
    public int StaffId { get; init; }

    [JsonPropertyName("full_name")]
    public string FullName { get; init; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("department_id")]
    public int? DepartmentId { get; init; }

    [JsonPropertyName("department_name")]
    public string DepartmentName { get; init; } = string.Empty;

    [JsonPropertyName("account_status")]
    public string AccountStatus { get; init; } = string.Empty;
}

public sealed class StaffAccountSaveRequest
{
    [JsonPropertyName("full_name")]
    public string FullName { get; init; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("department_id")]
    public int DepartmentId { get; init; }

    [JsonPropertyName("account_status")]
    public string AccountStatus { get; init; } = "ACTIVE";

    [JsonPropertyName("password")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Password { get; init; }
}