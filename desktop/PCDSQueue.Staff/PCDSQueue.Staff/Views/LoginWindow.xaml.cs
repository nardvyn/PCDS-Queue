using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PCDSQueue.Staff.Helpers;
using PCDSQueue.Staff.Services;

namespace PCDSQueue.Staff.Views;

public partial class LoginWindow : Window
{
    private readonly AuthService _authService = new();
    private readonly DispatcherTimer _serverTimer = new();
    private bool _serverOnline;
    private bool _isLoading;

    public LoginWindow()
    {
        InitializeComponent();
        Loaded += LoginWindow_Loaded;

        _serverTimer.Interval = TimeSpan.FromSeconds(3);
        _serverTimer.Tick += ServerTimer_Tick;
    }

    private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UsernameTextBox.Focus();
        await CheckServerAsync();
        _serverTimer.Start();
    }

    private async void ServerTimer_Tick(object? sender, EventArgs e) => await CheckServerAsync();

    private async Task CheckServerAsync()
    {
        _serverOnline = await ApiService.IsServerOnlineAsync();

        var statusColor = _serverOnline
            ? Color.FromRgb(22, 163, 106)
            : Color.FromRgb(220, 53, 69);

        ServerStatusDot.Fill = new SolidColorBrush(statusColor);
        ServerStatusText.Text = _serverOnline ? "Server Online" : "Server Offline";
        ServerStatusText.Foreground = new SolidColorBrush(statusColor);
        SignInButton.IsEnabled = _serverOnline && !_isLoading;
    }

    private async void SignInButton_Click(object sender, RoutedEventArgs e) => await LoginAsync();

    private async void PasswordInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await LoginAsync();
        }
    }

    private async Task LoginAsync()
    {
        if (!_serverOnline)
        {
            ShowError("PCDS Queue Server is offline. Please contact the administrator.");
            return;
        }

        HideError();
        var username = UsernameTextBox.Text.Trim();
        var password = PasswordInput.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowError("Please enter your username.");
            UsernameTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError("Please enter your password.");
            PasswordInput.Focus();
            return;
        }

        SetLoading(true);
        try
        {
            var result = await _authService.LoginAsync(username, password);
            if (result.User is null)
            {
                throw new Exception("Staff information was not returned.");
            }

            if (!string.Equals(result.User.Role, "STAFF", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("This account does not have Staff access.");
            }

            SessionManager.AccessToken = result.AccessToken;
            SessionManager.CurrentUser = result.User;
            ApiService.SetToken(result.AccessToken);

            new SelectWindowWindow().Show();
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool isLoading)
    {
        _isLoading = isLoading;
        SignInButton.IsEnabled = _serverOnline && !_isLoading;
        UsernameTextBox.IsEnabled = !isLoading;
        PasswordInput.IsEnabled = !isLoading;
        LoadingPanel.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override void OnClosed(EventArgs e)
    {
        _serverTimer.Stop();
        base.OnClosed(e);
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorPanel.Visibility = Visibility.Visible;
    }

    private void HideError() => ErrorPanel.Visibility = Visibility.Collapsed;
}
