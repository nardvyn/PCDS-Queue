using System.Text.Json.Serialization;

namespace PCDSQueue.Admin.Models;

public sealed class SystemSettingsResponse
{
    [JsonPropertyName("settings")]
    public SystemSettingsModel Settings { get; init; } = new();

    [JsonPropertyName("system_info")]
    public SystemInformationModel SystemInfo { get; init; } = new();
}

public sealed class SystemSettingsModel
{
    [JsonPropertyName("queue_digits")]
    public int QueueDigits { get; set; } = 3;

    [JsonPropertyName("qr_expiration_hours")]
    public int QrExpirationHours { get; set; } = 24;

    [JsonPropertyName("tv_voice_enabled")]
    public bool TvVoiceEnabled { get; set; } = true;

    [JsonPropertyName("refresh_interval")]
    public int RefreshInterval { get; set; } = 3;

    [JsonPropertyName("mobile_queue_enabled")]
    public bool MobileQueueEnabled { get; set; } = true;

    [JsonPropertyName("kiosk_queue_enabled")]
    public bool KioskQueueEnabled { get; set; } = true;
}

public sealed class SystemInformationModel
{
    [JsonPropertyName("api_server")]
    public string ApiServer { get; init; } = "Unknown";

    [JsonPropertyName("database")]
    public string Database { get; init; } = "Unknown";

    [JsonPropertyName("application")]
    public string Application { get; init; } = "PCDS Queue Management System";

    [JsonPropertyName("environment")]
    public string Environment { get; init; } = "Development";
}

public sealed class SystemHealthModel
{
    [JsonIgnore]
    public string ApiServer { get; init; } = "Offline";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "offline";

    [JsonPropertyName("database")]
    public string Database { get; init; } = "disconnected";
}