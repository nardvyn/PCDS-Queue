using System.Text.Json.Serialization;

namespace PCDSQueue.Admin.Models;

public sealed class QrSessionListResponse
{
    [JsonPropertyName("sessions")]
    public List<QrSessionModel> Sessions { get; init; } = [];
}

public sealed class QrSessionModel
{
    [JsonPropertyName("qr_session_id")]
    public long QrSessionId { get; init; }

    [JsonPropertyName("department_id")]
    public int DepartmentId { get; init; }

    [JsonPropertyName("department_name")]
    public string DepartmentName { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; init; }

    [JsonPropertyName("revoked_at")]
    public DateTime? RevokedAt { get; init; }

    [JsonIgnore]
    public string CreatedDisplay => CreatedAt?.ToString("MMM d, yyyy h:mm tt") ?? "---";

    [JsonIgnore]
    public string ExpiresDisplay => ExpiresAt?.ToString("MMM d, yyyy h:mm tt") ?? "---";
}

public sealed class QrGenerateResponse
{
    [JsonPropertyName("session")]
    public GeneratedQrSession Session { get; init; } = new();
}

public sealed class GeneratedQrSession
{
    [JsonPropertyName("qr_session_id")]
    public long QrSessionId { get; init; }

    [JsonPropertyName("department_id")]
    public int DepartmentId { get; init; }

    [JsonPropertyName("department_name")]
    public string DepartmentName { get; init; } = string.Empty;

    [JsonPropertyName("token")]
    public string Token { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; init; }
}