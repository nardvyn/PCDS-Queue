using System.Text.Json.Serialization;

namespace PCDSQueue.Staff.Models;

public sealed class QueueItemModel
{
    [JsonPropertyName("queue_id")]
    public long QueueId { get; init; }

    [JsonPropertyName("queue_number")]
    public string QueueNumber { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("window_id")]
    public int? WindowId { get; init; }
}

public sealed class QueueStatusResponse
{
    [JsonPropertyName("waiting_count")]
    public int WaitingCount { get; init; }

    [JsonPropertyName("next_waiting")]
    public QueueItemModel? NextWaiting { get; init; }

    [JsonPropertyName("serving")]
    public List<QueueItemModel> Serving { get; init; } = [];
}

public sealed class QueueDashboardResponse
{
    [JsonPropertyName("department")]
    public string Department { get; init; } = string.Empty;

    [JsonPropertyName("window")]
    public QueueWindowModel? Window { get; init; }

    [JsonPropertyName("current")]
    public QueueItemModel? Current { get; init; }

    [JsonPropertyName("waiting_count")]
    public int WaitingCount { get; init; }

    [JsonPropertyName("next")]
    public QueueItemModel? Next { get; init; }
}

public sealed class QueueWindowModel
{
    [JsonPropertyName("window_id")]
    public int WindowId { get; init; }

    [JsonPropertyName("window_number")]
    public int WindowNumber { get; init; }

    [JsonPropertyName("window_name")]
    public string WindowName { get; init; } = string.Empty;
}
