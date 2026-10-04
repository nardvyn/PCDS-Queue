using System.Text.Json.Serialization;

namespace PCDSQueue.Admin.Models;

public sealed class AdminQueueHistoryResponse
{
    [JsonPropertyName("history")]
    public List<AdminQueueHistoryItem> History { get; init; } = [];
}

public sealed class AdminQueueHistoryItem
{
    [JsonPropertyName("queue_id")]
    public long QueueId { get; init; }

    [JsonPropertyName("queue_number")]
    public string QueueNumber { get; init; } = string.Empty;

    [JsonPropertyName("queue_date")]
    public DateTime? QueueDate { get; init; }

    [JsonPropertyName("department_id")]
    public int DepartmentId { get; init; }

    [JsonPropertyName("department_name")]
    public string DepartmentName { get; init; } = string.Empty;

    [JsonPropertyName("window_number")]
    public int? WindowNumber { get; init; }

    [JsonPropertyName("window_name")]
    public string? WindowName { get; init; }

    [JsonPropertyName("staff_name")]
    public string? StaffName { get; init; }

    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    [JsonPropertyName("called_at")]
    public DateTime? CalledAt { get; init; }

    [JsonPropertyName("serving_at")]
    public DateTime? ServingAt { get; init; }

    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; init; }

    [JsonIgnore]
    public string WindowDisplay => WindowNumber is int number ? $"W{number:00}" : "---";

    [JsonIgnore]
    public string StaffDisplay => string.IsNullOrWhiteSpace(StaffName) ? "---" : StaffName;

    [JsonIgnore]
    public string CreatedDisplay => CreatedAt?.ToString("h:mm:ss tt") ?? "---";
}

public sealed class AdminQueueHistoryDetailResponse
{
    [JsonPropertyName("transaction")]
    public AdminQueueHistoryDetail Transaction { get; init; } = new();

    [JsonPropertyName("events")]
    public List<AdminQueueHistoryEvent> Events { get; init; } = [];
}

public sealed class AdminQueueHistoryDetail
{
    [JsonPropertyName("queue_id")]
    public long QueueId { get; init; }

    [JsonPropertyName("queue_number")]
    public string QueueNumber { get; init; } = string.Empty;

    [JsonPropertyName("queue_date")]
    public DateTime? QueueDate { get; init; }

    [JsonPropertyName("department_name")]
    public string DepartmentName { get; init; } = string.Empty;

    [JsonPropertyName("window_name")]
    public string? WindowName { get; init; }

    [JsonPropertyName("staff_name")]
    public string? StaffName { get; init; }

    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    [JsonPropertyName("called_at")]
    public DateTime? CalledAt { get; init; }

    [JsonPropertyName("serving_at")]
    public DateTime? ServingAt { get; init; }

    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; init; }

    [JsonPropertyName("waiting_seconds")]
    public int? WaitingSeconds { get; init; }

    [JsonPropertyName("service_seconds")]
    public int? ServiceSeconds { get; init; }

    [JsonIgnore]
    public string WindowDisplay => string.IsNullOrWhiteSpace(WindowName) ? "---" : WindowName;

    [JsonIgnore]
    public string StaffDisplay => string.IsNullOrWhiteSpace(StaffName) ? "---" : StaffName;

    [JsonIgnore]
    public string CreatedDisplay => CreatedAt?.ToString("h:mm:ss tt") ?? "---";

    [JsonIgnore]
    public string CalledDisplay => CalledAt?.ToString("h:mm:ss tt") ?? "---";

    [JsonIgnore]
    public string ServingDisplay => ServingAt?.ToString("h:mm:ss tt") ?? "---";

    [JsonIgnore]
    public string CompletedDisplay => CompletedAt?.ToString("h:mm:ss tt") ?? "---";

    [JsonIgnore]
    public string WaitingDurationDisplay => FormatDuration(WaitingSeconds);

    [JsonIgnore]
    public string ServiceDurationDisplay => FormatDuration(ServiceSeconds);

    private static string FormatDuration(int? seconds)
    {
        if (seconds is null)
            return "---";

        var duration = TimeSpan.FromSeconds(seconds.Value);
        return duration.TotalMinutes >= 1
            ? $"{(int)duration.TotalMinutes}m {duration.Seconds}s"
            : $"{duration.Seconds}s";
    }
}

public sealed class AdminQueueHistoryEvent
{
    [JsonPropertyName("event_id")]
    public long EventId { get; init; }

    [JsonPropertyName("event_type")]
    public string EventType { get; init; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    [JsonPropertyName("staff_name")]
    public string? StaffName { get; init; }

    [JsonPropertyName("window_name")]
    public string? WindowName { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    [JsonIgnore]
    public string TimeDisplay => CreatedAt?.ToString("h:mm:ss tt") ?? "---";

    [JsonIgnore]
    public string StaffDisplay => string.IsNullOrWhiteSpace(StaffName) ? "---" : StaffName;

    [JsonIgnore]
    public string WindowDisplay => string.IsNullOrWhiteSpace(WindowName) ? "---" : WindowName;
}