using System.Text.Json.Serialization;

namespace PCDSQueue.Kiosk.Models;

public sealed class DepartmentModel
{
    [JsonPropertyName("department_id")]
    public int DepartmentId { get; init; }

    [JsonPropertyName("department_name")]
    public string DepartmentName { get; init; } = string.Empty;

    [JsonPropertyName("queue_prefix")]
    public string QueuePrefix { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("queue_is_open")]
    public bool QueueIsOpen { get; init; }

    [JsonPropertyName("kiosk_queue_enabled")]
    public bool KioskQueueEnabled { get; init; } = true;

    public bool IsAvailableForKiosk => QueueIsOpen && KioskQueueEnabled;
}

public sealed class DepartmentResponse
{
    [JsonPropertyName("departments")]
    public List<DepartmentModel> Departments { get; init; } = [];
}
