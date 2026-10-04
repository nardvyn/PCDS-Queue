using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PCDSQueue.Admin.Helpers;
using PCDSQueue.Admin.Services;

namespace PCDSQueue.Admin.Views;

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
        if (IsVisible)
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
        LoginButton.IsEnabled = _serverOnline && !_isLoading;
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e) => await LoginAsync();

    private async void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
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

        ErrorText.Visibility = Visibility.Collapsed;
        var username = UsernameTextBox.Text.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ShowError("Enter your username and password.");
            return;
        }

        SetLoading(true);
        try
        {
            var result = await _authService.LoginAsync(username, password);
            if (result.User is null)
            {
                throw new Exception("Unable to load administrator account.");
            }

            if (!string.Equals(result.User.Role, "ADMIN", StringComparison.OrdinalIgnoreCase))
            {
                ShowError("This account does not have administrator access.");
                return;
            }

            SessionManager.StartSession(result.AccessToken, result.User);
            ApiService.SetToken(result.AccessToken);
            new AdminDashboardWindow().Show();
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            if (IsVisible)
                SetLoading(false);
        }
    }

    private void SetLoading(bool isLoading)
    {
        _isLoading = isLoading;
        LoginButton.IsEnabled = _serverOnline && !_isLoading;
        UsernameTextBox.IsEnabled = !isLoading;
        PasswordBox.IsEnabled = !isLoading;
        LoginButton.Content = isLoading ? "SIGNING IN..." : "LOGIN AS ADMINISTRATOR";
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    protected override void OnClosed(EventArgs e)
    {
        _serverTimer.Stop();
        base.OnClosed(e);
    }
}