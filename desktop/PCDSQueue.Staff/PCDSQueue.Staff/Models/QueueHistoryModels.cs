using System.Text.Json.Serialization;

namespace PCDSQueue.Staff.Models;

public sealed class QueueHistoryResponse
{
    [JsonPropertyName("history")]
    public List<QueueHistoryItem> History { get; init; } = [];
}

public sealed class QueueHistoryItem
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

    [JsonPropertyName("staff_name")]
    public string? StaffName { get; init; }

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    [JsonPropertyName("called_at")]
    public DateTime? CalledAt { get; init; }

    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; init; }

    public string WindowDisplay => WindowNumber is int number
        ? $"W{number:00}"
        : "---";

    public string CalledDisplay => CalledAt?.ToString("h:mm tt") ?? "---";

    public string CompletedDisplay => CompletedAt?.ToString("h:mm tt") ?? "---";
}
