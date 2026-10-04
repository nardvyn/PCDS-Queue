using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using PCDSQueue.TVDisplay.Models;

namespace PCDSQueue.TVDisplay.Services;

public sealed class ApiService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _client;

    public ApiService()
    {
        var baseAddress = Environment.GetEnvironmentVariable("PCDS_QUEUE_API_URL")?.Trim();
        if (string.IsNullOrWhiteSpace(baseAddress))
            baseAddress = "http://127.0.0.1:5000/";
        else if (!baseAddress.EndsWith('/'))
            baseAddress += "/";

        _client = new HttpClient
        {
            BaseAddress = new Uri(baseAddress, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(4)
        };
    }

    public async Task<DisplayQueueResponse> GetDisplayAsync()
    {
        using var response = await _client.GetAsync("api/queue/display");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DisplayQueueResponse>(SerializerOptions)
            ?? new DisplayQueueResponse();
    }

    public async Task<AnnouncementEventsResponse> GetAnnouncementEventsAsync(long afterEventId)
    {
        using var response = await _client.GetAsync(
            $"api/queue/announcement-events?after_event_id={afterEventId}");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AnnouncementEventsResponse>(SerializerOptions)
            ?? new AnnouncementEventsResponse();
    }
}