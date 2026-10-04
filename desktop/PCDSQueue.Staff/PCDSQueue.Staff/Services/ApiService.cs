using System.Net.Http;
using System.Net.Http.Headers;

namespace PCDSQueue.Staff.Services;

public static class ApiService
{
    public static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri(GetBaseAddress(), UriKind.Absolute),
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static string GetBaseAddress()
    {
        var address = Environment.GetEnvironmentVariable("PCDS_QUEUE_API_URL")?.Trim();
        if (string.IsNullOrWhiteSpace(address))
            return "http://127.0.0.1:5000/";

        return address.EndsWith('/') ? address : $"{address}/";
    }

    public static void SetToken(string token) =>
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    public static void ClearToken() =>
        Client.DefaultRequestHeaders.Authorization = null;

    public static async Task<bool> IsServerOnlineAsync()
    {
        try
        {
            using var cancellationTokenSource =
                new CancellationTokenSource(TimeSpan.FromSeconds(2));

            var response = await Client.GetAsync(
                "api/health",
                cancellationTokenSource.Token);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
