using System.Text.Json.Serialization;

namespace PCDSQueue.Kiosk.Models;

public sealed class GenerateQueueRequest
{
    [JsonPropertyName("department_id")]
    public int DepartmentId { get; init; }

    [JsonPropertyName("source")]
    public string Source { get; init; } = "KIOSK";
}

public sealed class GenerateQueueResponse
{
    [JsonPropertyName("queue")]
    public QueueTicketModel? Queue { get; init; }
}

public sealed class QueueTicketModel
{
    [JsonPropertyName("queue_id")]
    public long QueueId { get; init; }

    [JsonPropertyName("queue_number")]
    public string QueueNumber { get; init; } = string.Empty;

    [JsonPropertyName("sequence_number")]
    public int SequenceNumber { get; init; }

    [JsonPropertyName("department")]
    public QueueDepartmentModel? Department { get; init; }

    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("people_ahead")]
    public int PeopleAhead { get; init; }
}

public sealed class QueueDepartmentModel
{
    [JsonPropertyName("department_id")]
    public int DepartmentId { get; init; }

    [JsonPropertyName("department_name")]
    public string DepartmentName { get; init; } = string.Empty;

    [JsonPropertyName("queue_prefix")]
    public string QueuePrefix { get; init; } = string.Empty;
}