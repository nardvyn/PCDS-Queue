using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCDSQueue.TVDisplay.Models;

public sealed class AnnouncementEventsResponse
{
    [JsonPropertyName("events")]
    public List<AnnouncementEventModel> Events { get; init; } = [];

    [JsonPropertyName("latest_event_id")]
    public long LatestEventId { get; init; }

    [JsonPropertyName("next_after_event_id")]
    public long NextAfterEventId { get; init; }
}

public sealed class AnnouncementEventModel
{
    [JsonPropertyName("event_id")]
    public long EventId { get; init; }

    [JsonPropertyName("queue_id")]
    public long QueueId { get; init; }

    [JsonPropertyName("event_type")]
    public string EventType { get; init; } = string.Empty;

    [JsonPropertyName("queue_number")]
    public string QueueNumber { get; init; } = string.Empty;

    [JsonPropertyName("department_name")]
    public string DepartmentName { get; init; } = string.Empty;

    [JsonPropertyName("window_number")]
    public int? WindowNumber { get; init; }

    [JsonPropertyName("window_name")]
    public string? WindowName { get; init; }

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }
}