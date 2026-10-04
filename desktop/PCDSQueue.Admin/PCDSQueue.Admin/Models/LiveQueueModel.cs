using System.Text.Json.Serialization;

namespace PCDSQueue.Admin.Models;

public sealed class LiveQueueResponse
{
    [JsonPropertyName("summary")]
    public LiveQueueSummary Summary { get; init; } = new();

    [JsonPropertyName("serving")]
    public List<LiveServingItem> Serving { get; init; } = [];

    [JsonPropertyName("waiting")]
    public List<LiveWaitingItem> Waiting { get; init; } = [];
}

public sealed class LiveQueueSummary
{
    [JsonPropertyName("waiting")]
    public int Waiting { get; init; }

    [JsonPropertyName("serving")]
    public int Serving { get; init; }

    [JsonPropertyName("windows_in_use")]
    public int WindowsInUse { get; init; }
}

public sealed class LiveServingItem
{
    [JsonPropertyName("queue_id")]
    public long QueueId { get; init; }

    [JsonPropertyName("queue_number")]
    public string QueueNumber { get; init; } = string.Empty;

    [JsonPropertyName("department_name")]
    public string DepartmentName { get; init; } = string.Empty;

    [JsonPropertyName("window_number")]
    public int? WindowNumber { get; init; }

    [JsonPropertyName("window_name")]
    public string? WindowName { get; init; }

    [JsonPropertyName("staff_name")]
    public string? StaffName { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("serving_at")]
    public DateTime? ServingAt { get; init; }

    [JsonIgnore]
    public string WindowDisplay => WindowNumber is null ? "---" : $"W{WindowNumber:00}";

    [JsonIgnore]
    public string StaffDisplay => string.IsNullOrWhiteSpace(StaffName) ? "---" : StaffName;
}

public sealed class LiveWaitingItem
{
    [JsonPropertyName("queue_id")]
    public long QueueId { get; init; }

    [JsonPropertyName("queue_number")]
    public string QueueNumber { get; init; } = string.Empty;

    [JsonPropertyName("department_name")]
    public string DepartmentName { get; init; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    [JsonIgnore]
    public string SourceDisplay => Source == "TICKET" ? "TICKET / KIOSK" : Source;

    [JsonIgnore]
    public string CreatedDisplay => CreatedAt?.ToString("h:mm tt") ?? "---";
}