using System.Text.Json.Serialization;

namespace PCDSQueue.TVDisplay.Models;

public sealed class DisplayQueueResponse
{
    [JsonPropertyName("tv_voice_enabled")]
    public bool TvVoiceEnabled { get; init; } = true;

    [JsonPropertyName("refresh_interval")]
    public int RefreshInterval { get; init; } = 3;

    [JsonPropertyName("departments")]
    public List<DisplayDepartmentModel> Departments { get; init; } = [];
}

public sealed class DisplayDepartmentModel
{
    [JsonPropertyName("department_id")]
    public int DepartmentId { get; init; }

    [JsonPropertyName("department_name")]
    public string DepartmentName { get; init; } = string.Empty;

    [JsonPropertyName("queue_prefix")]
    public string QueuePrefix { get; init; } = string.Empty;

    [JsonPropertyName("waiting_count")]
    public int WaitingCount { get; init; }

    [JsonPropertyName("serving")]
    public List<DisplayServingQueueModel> Serving { get; init; } = [];
}

public sealed class DisplayServingQueueModel
{
    [JsonPropertyName("queue_id")]
    public long QueueId { get; init; }

    [JsonPropertyName("queue_number")]
    public string QueueNumber { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("window_number")]
    public int? WindowNumber { get; init; }

    [JsonPropertyName("window_name")]
    public string? WindowName { get; init; }
}

public sealed class DisplayQueueModel
{
    public long QueueId { get; init; }

    public string QueueNumber { get; init; } = string.Empty;

    public string DepartmentName { get; init; } = string.Empty;

    public int? WindowNumber { get; init; }

    public string? WindowName { get; init; }

    public bool IsAnnouncementHighlighted { get; set; }

    public string WindowDisplay => WindowNumber is null
        ? WindowName ?? "WINDOW"
        : $"WINDOW {WindowNumber:00}";
}