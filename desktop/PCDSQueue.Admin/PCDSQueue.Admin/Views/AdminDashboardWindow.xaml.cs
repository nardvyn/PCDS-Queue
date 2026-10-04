using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using PCDSQueue.Admin.Helpers;
using PCDSQueue.Admin.Services;

namespace PCDSQueue.Admin.Views;

public partial class AdminDashboardWindow : Window
{
    private readonly AdminDashboardService _dashboardService = new();
    private readonly DispatcherTimer _serverTimer = new();
    private readonly DispatcherTimer _refreshTimer = new();
    private bool _serverOnline;
    private bool _checkingServer;
    private bool _isRefreshing;

    public AdminDashboardWindow()
    {
        InitializeComponent();

        AdminNameText.Text = SessionManager.CurrentUser?.FullName ?? "Administrator";
        AdminUsernameText.Text = $"@{SessionManager.CurrentUser?.Username ?? "admin"}";

        _serverTimer.Interval = TimeSpan.FromSeconds(3);
        _serverTimer.Tick += ServerTimer_Tick;
        _refreshTimer.Interval = TimeSpan.FromSeconds(5);
        _refreshTimer.Tick += RefreshTimer_Tick;
        Loaded += AdminDashboardWindow_Loaded;
    }

    private async void AdminDashboardWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await CheckServerAsync();
        if (IsVisible)
            _serverTimer.Start();
    }

    private async void ServerTimer_Tick(object? sender, EventArgs e) => await CheckServerAsync();

    private async void RefreshTimer_Tick(object? sender, EventArgs e) => await LoadDashboardAsync();

    private async Task CheckServerAsync()
    {
        if (_checkingServer)
            return;

        _checkingServer = true;
        try
        {
            var online = await ApiService.IsServerOnlineAsync();
            var statusColor = online
                ? Color.FromRgb(22, 163, 106)
                : Color.FromRgb(220, 53, 69);

            ServerStatusDot.Fill = new SolidColorBrush(statusColor);
            ServerStatusText.Text = online ? "Server Online" : "Server Offline";
            ServerStatusText.Foreground = new SolidColorBrush(statusColor);

            if (online)
            {
                if (!_serverOnline)
                {
                    _serverOnline = true;
                    await LoadDashboardAsync();
                    if (IsVisible)
                        _refreshTimer.Start();
                }
            }
            else
            {
                _serverOnline = false;
                _refreshTimer.Stop();
                ShowDashboardError("The server or database is unavailable.");
            }
        }
        finally
        {
            _checkingServer = false;
        }
    }

    private async Task LoadDashboardAsync()
    {
        if (!_serverOnline || _isRefreshing)
            return;

        _isRefreshing = true;
        try
        {
            var dashboard = await _dashboardService.GetDashboardAsync();
            WaitingTodayText.Text = dashboard.Stats.WaitingToday.ToString("N0");
            CurrentlyServingText.Text = dashboard.Stats.CurrentlyServing.ToString("N0");
            ActiveWindowsText.Text = dashboard.Stats.ActiveWindows.ToString("N0");
            DepartmentsText.Text = dashboard.Stats.DepartmentCount.ToString("N0");
            DepartmentStatusGrid.ItemsSource = dashboard.Departments;
            DashboardErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ShowDashboardError(ex.Message);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void ShowDashboardError(string message)
    {
        DashboardErrorText.Text = message;
        DashboardErrorText.Visibility = Visibility.Visible;
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        SessionManager.Clear();
        new LoginWindow().Show();
        Close();
    }

    private async void DepartmentsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPage.Visibility = Visibility.Collapsed;
        SettingsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        SettingsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        _refreshTimer.Stop();
        DashboardPage.Visibility = Visibility.Collapsed;
        DepartmentsPage.Visibility = Visibility.Visible;
        ServiceWindowsPage.Visibility = Visibility.Collapsed;
        StaffAccountsPage.Visibility = Visibility.Collapsed;
        LiveQueuePage.Visibility = Visibility.Collapsed;
        QueueHistoryPage.Visibility = Visibility.Collapsed;
        ReportsPage.Visibility = Visibility.Collapsed;
        QrManagementPage.Visibility = Visibility.Collapsed;
        LiveQueuePage.StopPolling();
        PageTitleText.Text = "DEPARTMENTS";
        PageSubtitleText.Text = "Configure campus departments and service windows";
        DashboardNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        DashboardNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        DepartmentsNavigationButton.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1D478B")!;
        DepartmentsNavigationButton.Foreground = System.Windows.Media.Brushes.White;
        ServiceWindowsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        ServiceWindowsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        StaffAccountsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        StaffAccountsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        LiveQueueNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        LiveQueueNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        QueueHistoryNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        QueueHistoryNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        ReportsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        ReportsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        QrManagementNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        QrManagementNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        await DepartmentsPage.RefreshAsync();
    }

    private async void ServiceWindowsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPage.Visibility = Visibility.Collapsed;
        SettingsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        SettingsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        _refreshTimer.Stop();
        DashboardPage.Visibility = Visibility.Collapsed;
        DepartmentsPage.Visibility = Visibility.Collapsed;
        ServiceWindowsPage.Visibility = Visibility.Visible;
        StaffAccountsPage.Visibility = Visibility.Collapsed;
        LiveQueuePage.Visibility = Visibility.Collapsed;
        QueueHistoryPage.Visibility = Visibility.Collapsed;
        ReportsPage.Visibility = Visibility.Collapsed;
        QrManagementPage.Visibility = Visibility.Collapsed;
        LiveQueuePage.StopPolling();
        PageTitleText.Text = "SERVICE WINDOWS";
        PageSubtitleText.Text = "Configure windows and monitor current staff availability";
        DashboardNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        DashboardNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        DepartmentsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        DepartmentsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        ServiceWindowsNavigationButton.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1D478B")!;
        ServiceWindowsNavigationButton.Foreground = System.Windows.Media.Brushes.White;
        StaffAccountsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        StaffAccountsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        LiveQueueNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        LiveQueueNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        QueueHistoryNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        QueueHistoryNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        ReportsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        ReportsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        QrManagementNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        QrManagementNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        await ServiceWindowsPage.RefreshAsync();
    }

    private async void StaffAccountsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPage.Visibility = Visibility.Collapsed;
        SettingsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        SettingsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        _refreshTimer.Stop();
        DashboardPage.Visibility = Visibility.Collapsed;
        DepartmentsPage.Visibility = Visibility.Collapsed;
        ServiceWindowsPage.Visibility = Visibility.Collapsed;
        StaffAccountsPage.Visibility = Visibility.Visible;
        LiveQueuePage.Visibility = Visibility.Collapsed;
        QueueHistoryPage.Visibility = Visibility.Collapsed;
        ReportsPage.Visibility = Visibility.Collapsed;
        QrManagementPage.Visibility = Visibility.Collapsed;
        LiveQueuePage.StopPolling();
        PageTitleText.Text = "STAFF ACCOUNTS";
        PageSubtitleText.Text = "Manage Staff logins and department assignments";
        DashboardNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        DashboardNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        DepartmentsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        DepartmentsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        ServiceWindowsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        ServiceWindowsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        LiveQueueNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        LiveQueueNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        QueueHistoryNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        QueueHistoryNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        ReportsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        ReportsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        QrManagementNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        QrManagementNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        StaffAccountsNavigationButton.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1D478B")!;
        StaffAccountsNavigationButton.Foreground = System.Windows.Media.Brushes.White;
        await StaffAccountsPage.RefreshAsync();
    }

    private async void LiveQueueButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPage.Visibility = Visibility.Collapsed;
        SettingsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        SettingsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        _refreshTimer.Stop();
        DashboardPage.Visibility = Visibility.Collapsed;
        DepartmentsPage.Visibility = Visibility.Collapsed;
        ServiceWindowsPage.Visibility = Visibility.Collapsed;
        StaffAccountsPage.Visibility = Visibility.Collapsed;
        QueueHistoryPage.Visibility = Visibility.Collapsed;
        ReportsPage.Visibility = Visibility.Collapsed;
        QrManagementPage.Visibility = Visibility.Collapsed;
        LiveQueuePage.Visibility = Visibility.Visible;
        PageTitleText.Text = "LIVE QUEUE";
        PageSubtitleText.Text = "Read-only campus queue and service-window monitoring";
        DashboardNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        DashboardNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        DepartmentsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        DepartmentsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        ServiceWindowsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        ServiceWindowsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        StaffAccountsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        StaffAccountsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        LiveQueueNavigationButton.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1D478B")!;
        LiveQueueNavigationButton.Foreground = System.Windows.Media.Brushes.White;
        QueueHistoryNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        QueueHistoryNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        ReportsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        ReportsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        QrManagementNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        QrManagementNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        await LiveQueuePage.RefreshAsync();
    }

    private async void QueueHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPage.Visibility = Visibility.Collapsed;
        SettingsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        SettingsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        _refreshTimer.Stop();
        LiveQueuePage.StopPolling();
        DashboardPage.Visibility = Visibility.Collapsed;
        DepartmentsPage.Visibility = Visibility.Collapsed;
        ServiceWindowsPage.Visibility = Visibility.Collapsed;
        StaffAccountsPage.Visibility = Visibility.Collapsed;
        LiveQueuePage.Visibility = Visibility.Collapsed;
        QueueHistoryPage.Visibility = Visibility.Visible;
        ReportsPage.Visibility = Visibility.Collapsed;
        QrManagementPage.Visibility = Visibility.Collapsed;
        PageTitleText.Text = "QUEUE HISTORY";
        PageSubtitleText.Text = "Campus-wide queue transactions and lifecycle details";
        DashboardNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        DashboardNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        DepartmentsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        DepartmentsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        ServiceWindowsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        ServiceWindowsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        StaffAccountsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        StaffAccountsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        LiveQueueNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        LiveQueueNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        ReportsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        ReportsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        QrManagementNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        QrManagementNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        QueueHistoryNavigationButton.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1D478B")!;
        QueueHistoryNavigationButton.Foreground = System.Windows.Media.Brushes.White;
        await QueueHistoryPage.RefreshAsync();
    }

    private async void ReportsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPage.Visibility = Visibility.Collapsed;
        SettingsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        SettingsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        _refreshTimer.Stop();
        LiveQueuePage.StopPolling();
        DashboardPage.Visibility = Visibility.Collapsed;
        DepartmentsPage.Visibility = Visibility.Collapsed;
        ServiceWindowsPage.Visibility = Visibility.Collapsed;
        StaffAccountsPage.Visibility = Visibility.Collapsed;
        LiveQueuePage.Visibility = Visibility.Collapsed;
        QueueHistoryPage.Visibility = Visibility.Collapsed;
        ReportsPage.Visibility = Visibility.Visible;
        QrManagementPage.Visibility = Visibility.Collapsed;
        PageTitleText.Text = "REPORTS";
        PageSubtitleText.Text = "Queue performance and service analytics";
        DashboardNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        DashboardNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        DepartmentsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        DepartmentsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        ServiceWindowsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        ServiceWindowsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        StaffAccountsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        StaffAccountsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        LiveQueueNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        LiveQueueNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        QueueHistoryNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        QueueHistoryNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        ReportsNavigationButton.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1D478B")!;
        ReportsNavigationButton.Foreground = System.Windows.Media.Brushes.White;
        QrManagementNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        QrManagementNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        await ReportsPage.RefreshAsync();
    }

    private async void QrManagementButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPage.Visibility = Visibility.Collapsed;
        SettingsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        SettingsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        _refreshTimer.Stop();
        LiveQueuePage.StopPolling();
        DashboardPage.Visibility = Visibility.Collapsed;
        DepartmentsPage.Visibility = Visibility.Collapsed;
        ServiceWindowsPage.Visibility = Visibility.Collapsed;
        StaffAccountsPage.Visibility = Visibility.Collapsed;
        LiveQueuePage.Visibility = Visibility.Collapsed;
        QueueHistoryPage.Visibility = Visibility.Collapsed;
        ReportsPage.Visibility = Visibility.Collapsed;
        QrManagementPage.Visibility = Visibility.Visible;
        PageTitleText.Text = "QR MANAGEMENT";
        PageSubtitleText.Text = "Create and manage secure mobile queue access codes";
        DashboardNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        DashboardNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        DepartmentsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        DepartmentsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        ServiceWindowsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        ServiceWindowsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        StaffAccountsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        StaffAccountsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        LiveQueueNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        LiveQueueNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        QueueHistoryNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        QueueHistoryNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        ReportsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        ReportsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        QrManagementNavigationButton.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1D478B")!;
        QrManagementNavigationButton.Foreground = System.Windows.Media.Brushes.White;
        await QrManagementPage.RefreshAsync();
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
        LiveQueuePage.StopPolling();
        DashboardPage.Visibility = Visibility.Collapsed;
        DepartmentsPage.Visibility = Visibility.Collapsed;
        ServiceWindowsPage.Visibility = Visibility.Collapsed;
        StaffAccountsPage.Visibility = Visibility.Collapsed;
        LiveQueuePage.Visibility = Visibility.Collapsed;
        QueueHistoryPage.Visibility = Visibility.Collapsed;
        ReportsPage.Visibility = Visibility.Collapsed;
        QrManagementPage.Visibility = Visibility.Collapsed;
        SettingsPage.Visibility = Visibility.Visible;
        PageTitleText.Text = "SETTINGS";
        PageSubtitleText.Text = "System-wide queue configuration";
        DashboardNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        DashboardNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        DepartmentsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        DepartmentsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        ServiceWindowsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        ServiceWindowsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        StaffAccountsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        StaffAccountsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        LiveQueueNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        LiveQueueNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        QueueHistoryNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        QueueHistoryNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        ReportsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        ReportsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        QrManagementNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        QrManagementNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        SettingsNavigationButton.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1D478B")!;
        SettingsNavigationButton.Foreground = System.Windows.Media.Brushes.White;
        await SettingsPage.RefreshAsync();
    }

    private async void DashboardNavigationButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPage.Visibility = Visibility.Collapsed;
        SettingsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        SettingsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        DepartmentsPage.Visibility = Visibility.Collapsed;
        ServiceWindowsPage.Visibility = Visibility.Collapsed;
        StaffAccountsPage.Visibility = Visibility.Collapsed;
        LiveQueuePage.Visibility = Visibility.Collapsed;
        QueueHistoryPage.Visibility = Visibility.Collapsed;
        ReportsPage.Visibility = Visibility.Collapsed;
        QrManagementPage.Visibility = Visibility.Collapsed;
        LiveQueuePage.StopPolling();
        DashboardPage.Visibility = Visibility.Visible;
        PageTitleText.Text = "ADMIN DASHBOARD";
        PageSubtitleText.Text = "Campus queue overview for today";
        DashboardNavigationButton.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1D478B")!;
        DashboardNavigationButton.Foreground = System.Windows.Media.Brushes.White;
        DepartmentsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        DepartmentsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        ServiceWindowsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        ServiceWindowsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        StaffAccountsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        StaffAccountsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        LiveQueueNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        LiveQueueNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        QueueHistoryNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        QueueHistoryNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        ReportsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        ReportsNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        QrManagementNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
        QrManagementNavigationButton.Foreground = System.Windows.Media.Brushes.LightGray;
        await LoadDashboardAsync();
        if (_serverOnline && IsVisible)
            _refreshTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _serverTimer.Stop();
        _refreshTimer.Stop();
        base.OnClosed(e);
    }
}