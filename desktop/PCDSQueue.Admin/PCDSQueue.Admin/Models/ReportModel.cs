using System.Text.Json.Serialization;

namespace PCDSQueue.Admin.Models;

public sealed class ReportResponse
{
    [JsonPropertyName("date_from")]
    public string DateFrom { get; init; } = string.Empty;

    [JsonPropertyName("date_to")]
    public string DateTo { get; init; } = string.Empty;

    [JsonPropertyName("summary")]
    public ReportSummary Summary { get; init; } = new();

    [JsonPropertyName("departments")]
    public List<DepartmentPerformance> Departments { get; init; } = [];
}

public sealed class ReportSummary
{
    [JsonPropertyName("total_queues")]
    public int TotalQueues { get; init; }

    [JsonPropertyName("completed")]
    public int Completed { get; init; }

    [JsonPropertyName("no_show")]
    public int NoShow { get; init; }

    [JsonPropertyName("average_waiting_seconds")]
    public int AverageWaitingSeconds { get; init; }

    [JsonPropertyName("average_service_seconds")]
    public int AverageServiceSeconds { get; init; }
}

public sealed class DepartmentPerformance
{
    [JsonPropertyName("department_id")]
    public int DepartmentId { get; init; }

    [JsonPropertyName("department_name")]
    public string DepartmentName { get; init; } = string.Empty;

    [JsonPropertyName("total_queues")]
    public int TotalQueues { get; init; }

    [JsonPropertyName("completed")]
    public int Completed { get; init; }

    [JsonPropertyName("no_show")]
    public int NoShow { get; init; }

    [JsonPropertyName("average_waiting_seconds")]
    public int AverageWaitingSeconds { get; init; }

    [JsonIgnore]
    public string AverageWaitingDisplay => ReportTimeFormatter.Format(AverageWaitingSeconds);
}

public static class ReportTimeFormatter
{
    public static string Format(int seconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes}m {duration.Seconds}s";
        if (duration.TotalMinutes >= 1)
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        return $"{duration.Seconds}s";
    }
}