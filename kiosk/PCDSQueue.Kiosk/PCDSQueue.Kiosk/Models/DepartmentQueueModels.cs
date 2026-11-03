using System.Text.Json.Serialization;

namespace PCDSQueue.Kiosk.Models;

public sealed class KioskQrSessionResponse
{
    [JsonPropertyName("session_id")]
    public long SessionId { get; init; }

    [JsonPropertyName("token")]
    public string Token { get; init; } = string.Empty;

    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; init; }

    [JsonPropertyName("department")]
    public DepartmentModel? Department { get; init; }
}

public sealed class DepartmentQueueStatusResponse
{
    [JsonPropertyName("department")]
    public DepartmentModel? Department { get; init; }

    [JsonPropertyName("waiting_count")]
    public int WaitingCount { get; init; }

    [JsonPropertyName("active_queue_count")]
    public int ActiveQueueCount { get; init; }

    [JsonPropertyName("active_queue_limit")]
    public int ActiveQueueLimit { get; init; } = 50;

    [JsonPropertyName("daily_queue_count")]
    public int DailyQueueCount { get; init; }

    [JsonPropertyName("daily_queue_limit")]
    public int DailyQueueLimit { get; init; } = 100;
}

public sealed class KioskQrSessionStatusResponse
{
    [JsonPropertyName("scanned")]
    public bool Scanned { get; init; }
}