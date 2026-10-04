using System.Text.Json.Serialization;

namespace PCDSQueue.Admin.Models;

public sealed class ServiceWindowListResponse
{
    [JsonPropertyName("windows")]
    public List<ServiceWindowModel> Windows { get; init; } = [];
}

public sealed class ServiceWindowModel
{
    [JsonPropertyName("window_id")]
    public int WindowId { get; init; }

    [JsonPropertyName("department_id")]
    public int DepartmentId { get; init; }

    [JsonPropertyName("department_name")]
    public string DepartmentName { get; init; } = string.Empty;

    [JsonPropertyName("window_number")]
    public int WindowNumber { get; init; }

    [JsonPropertyName("window_name")]
    public string WindowName { get; init; } = string.Empty;

    [JsonPropertyName("is_active")]
    public bool IsActive { get; init; }

    [JsonPropertyName("is_in_use")]
    public bool IsInUse { get; init; }

    [JsonPropertyName("current_staff_name")]
    public string? CurrentStaffName { get; init; }

    [JsonIgnore]
    public string ConfiguredStatus => IsActive ? "ACTIVE" : "INACTIVE";

    [JsonIgnore]
    public string AvailabilityStatus => IsInUse ? "IN USE" : "AVAILABLE";

    [JsonIgnore]
    public string StaffDisplay => string.IsNullOrWhiteSpace(CurrentStaffName) ? "---" : CurrentStaffName;
}

public sealed class ServiceWindowCreateRequest
{
    [JsonPropertyName("department_id")]
    public int DepartmentId { get; init; }

    [JsonPropertyName("window_name")]
    public string? WindowName { get; init; }
}

public sealed class ServiceWindowUpdateRequest
{
    [JsonPropertyName("window_name")]
    public string WindowName { get; init; } = string.Empty;
}