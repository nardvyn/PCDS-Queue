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

    [JsonPropertyName("queue_daily_limit")]
    public int QueueDailyLimit { get; set; } = 100;

    [JsonPropertyName("queue_active_limit")]
    public int QueueActiveLimit { get; set; } = 50;

    [JsonPropertyName("call_grace_period_seconds")]
    public int CallGracePeriodSeconds { get; set; } = 60;

    [JsonPropertyName("enable_call_countdown")]
    public bool EnableCallCountdown { get; set; } = true;

    [JsonPropertyName("lock_no_show_during_grace")]
    public bool LockNoShowDuringGrace { get; set; } = true;

    [JsonPropertyName("allow_customer_acknowledgement")]
    public bool AllowCustomerAcknowledgement { get; set; } = true;

    [JsonPropertyName("qr_expiration_hours")]
    public int QrExpirationHours { get; set; } = 24;

    [JsonPropertyName("tv_voice_enabled")]
    public bool TvVoiceEnabled { get; set; } = true;

    [JsonPropertyName("mobile_call_alert")]
    public bool MobileCallAlert { get; set; } = true;

    [JsonPropertyName("mobile_recall_alert")]
    public bool MobileRecallAlert { get; set; } = true;

    [JsonPropertyName("mobile_voice_announcement")] public bool MobileVoiceAnnouncement { get; set; } = true;
    [JsonPropertyName("require_join_confirmation")] public bool RequireJoinConfirmation { get; set; } = true;
    [JsonPropertyName("allow_mobile_cancellation")] public bool AllowMobileCancellation { get; set; } = true;
    [JsonPropertyName("show_people_ahead")] public bool ShowPeopleAhead { get; set; } = true;
    [JsonPropertyName("show_estimated_wait_time")] public bool ShowEstimatedWaitTime { get; set; }
    [JsonPropertyName("keep_alert_until_acknowledged")] public bool KeepAlertUntilAcknowledged { get; set; }
    [JsonPropertyName("mobile_alert_duration_seconds")] public int MobileAlertDurationSeconds { get; set; } = 15;
    [JsonPropertyName("show_connection_warning")] public bool ShowConnectionWarning { get; set; } = true;
    [JsonPropertyName("automatic_reconnection")] public bool AutomaticReconnection { get; set; } = true;
    [JsonPropertyName("offline_retry_interval_seconds")] public int OfflineRetryIntervalSeconds { get; set; } = 5;

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
