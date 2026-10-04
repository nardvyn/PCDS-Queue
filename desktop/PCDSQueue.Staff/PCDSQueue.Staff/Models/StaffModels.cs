using System.Text.Json.Serialization;

namespace PCDSQueue.Staff.Models;

public sealed class DepartmentModel
{
    [JsonPropertyName("department_id")]
    public int DepartmentId { get; init; }

    [JsonPropertyName("department_name")]
    public string DepartmentName { get; init; } = string.Empty;
}

public sealed class WindowModel
{
    [JsonPropertyName("window_id")]
    public int WindowId { get; init; }

    [JsonPropertyName("window_number")]
    public int WindowNumber { get; init; }

    [JsonPropertyName("window_name")]
    public string WindowName { get; init; } = string.Empty;

    [JsonPropertyName("available")]
    public bool Available { get; init; }

    [JsonPropertyName("assigned_staff_name")]
    public string? AssignedStaffName { get; init; }
}

public sealed class WindowsResponse
{
    [JsonPropertyName("department")]
    public DepartmentModel? Department { get; init; }

    [JsonPropertyName("windows")]
    public List<WindowModel> Windows { get; init; } = [];
}

public sealed class ShiftModel
{
    [JsonPropertyName("shift_id")]
    public int ShiftId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("department_id")]
    public int DepartmentId { get; init; }

    [JsonPropertyName("department")]
    public string Department { get; init; } = string.Empty;

    [JsonPropertyName("department_name")]
    public string DepartmentName { get; init; } = string.Empty;

    [JsonPropertyName("window_id")]
    public int WindowId { get; init; }

    [JsonPropertyName("window_number")]
    public int WindowNumber { get; init; }

    [JsonPropertyName("window_name")]
    public string WindowName { get; init; } = string.Empty;

    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; init; }

    public string DisplayDepartment =>
        string.IsNullOrWhiteSpace(DepartmentName) ? Department : DepartmentName;
}

public sealed class ShiftResponse
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("shift")]
    public ShiftModel? Shift { get; init; }
}

public sealed class CurrentShiftResponse
{
    [JsonPropertyName("has_active_shift")]
    public bool HasActiveShift { get; init; }

    [JsonPropertyName("shift")]
    public ShiftModel? Shift { get; init; }
}
