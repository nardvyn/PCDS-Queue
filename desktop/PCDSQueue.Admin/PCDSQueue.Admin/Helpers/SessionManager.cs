using PCDSQueue.Admin.Models;
using PCDSQueue.Admin.Services;

namespace PCDSQueue.Admin.Helpers;

public static class SessionManager
{
    public static string? AccessToken { get; private set; }

    public static AdminUserModel? CurrentUser { get; private set; }

    public static bool IsLoggedIn =>
        !string.IsNullOrWhiteSpace(AccessToken) && CurrentUser is not null;

    public static void StartSession(string token, AdminUserModel user)
    {
        AccessToken = token;
        CurrentUser = user;
    }

    public static void Clear()
    {
        AccessToken = null;
        CurrentUser = null;
        ApiService.ClearToken();
    }
}