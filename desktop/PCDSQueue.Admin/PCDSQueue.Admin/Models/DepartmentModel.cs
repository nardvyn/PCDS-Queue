using System.Text.Json.Serialization;

namespace PCDSQueue.Admin.Models;

public sealed class DepartmentListResponse
{
    [JsonPropertyName("departments")]
    public List<DepartmentModel> Departments { get; init; } = [];
}

public sealed class DepartmentModel
{
    [JsonPropertyName("department_id")]
    public int DepartmentId { get; init; }

    [JsonPropertyName("department_name")]
    public string DepartmentName { get; init; } = string.Empty;

    [JsonPropertyName("queue_prefix")]
    public string QueuePrefix { get; init; } = string.Empty;

    [JsonPropertyName("window_count")]
    public int WindowCount { get; init; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; init; }

    [JsonIgnore]
    public string StatusText => IsActive ? "ACTIVE" : "INACTIVE";
}

public sealed class DepartmentSaveRequest
{
    [JsonPropertyName("department_name")]
    public string DepartmentName { get; init; } = string.Empty;

    [JsonPropertyName("queue_prefix")]
    public string QueuePrefix { get; init; } = string.Empty;

    [JsonPropertyName("window_count")]
    public int WindowCount { get; init; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; init; }
}