using System.Text.Json.Serialization;

namespace PCDSQueue.Admin.Models;

public sealed class AdminDashboardResponse
{
    [JsonPropertyName("stats")]
    public AdminDashboardStats Stats { get; init; } = new();

    [JsonPropertyName("departments")]
    public List<AdminDepartmentStatus> Departments { get; init; } = [];
}

public sealed class AdminDashboardStats
{
    [JsonPropertyName("waiting_today")]
    public int WaitingToday { get; init; }

    [JsonPropertyName("currently_serving")]
    public int CurrentlyServing { get; init; }

    [JsonPropertyName("active_windows")]
    public int ActiveWindows { get; init; }

    [JsonPropertyName("departments")]
    public int DepartmentCount { get; init; }
}

public sealed class AdminDepartmentStatus
{
    [JsonPropertyName("department_id")]
    public int DepartmentId { get; init; }

    [JsonPropertyName("department_name")]
    public string DepartmentName { get; init; } = string.Empty;

    [JsonPropertyName("active_windows")]
    public int ActiveWindows { get; init; }

    [JsonPropertyName("waiting_count")]
    public int WaitingCount { get; init; }
}