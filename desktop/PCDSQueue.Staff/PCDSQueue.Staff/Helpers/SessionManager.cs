using PCDSQueue.Staff.Models;

namespace PCDSQueue.Staff.Helpers;

public static class SessionManager
{
    public static string AccessToken { get; set; } = string.Empty;

    public static UserModel? CurrentUser { get; set; }

    public static ShiftModel? CurrentShift { get; set; }

    public static bool IsLoggedIn =>
        !string.IsNullOrWhiteSpace(AccessToken) && CurrentUser is not null;

    public static void Clear()
    {
        AccessToken = string.Empty;
        CurrentUser = null;
        CurrentShift = null;
    }
}
