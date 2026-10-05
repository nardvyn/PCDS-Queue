using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using PCDSQueue.Staff.Helpers;
using PCDSQueue.Staff.Models;
using PCDSQueue.Staff.Services;

namespace PCDSQueue.Staff.Views;

public partial class StaffDashboardWindow : Window
{
    private readonly StaffService _staffService = new();
    private readonly QueueService _queueService = new();
    private readonly DispatcherTimer _serverTimer = new();
    private readonly DispatcherTimer _refreshTimer = new()
    {
        Interval = TimeSpan.FromSeconds(3)
    };

    private ShiftModel? _shift;
    private QueueItemModel? _currentQueue;
    private bool _isRefreshing;
    private bool _serverOnline;
    private bool _checkingServer;

    public StaffDashboardWindow()
    {
        InitializeComponent();
        LoadUserInformation();
        DisableQueueControls();
        Loaded += StaffDashboardWindow_Loaded;
        _serverTimer.Interval = TimeSpan.FromSeconds(3);
        _serverTimer.Tick += ServerTimer_Tick;
        _refreshTimer.Tick += async (_, _) => await RefreshQueueAsync();
    }

    private async void StaffDashboardWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await CheckServerAsync();
        if (IsVisible)
            _serverTimer.Start();
    }

    private async void ServerTimer_Tick(object? sender, EventArgs e)
    {
        await CheckServerAsync();
    }

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
                    await LoadCurrentShiftAsync();

                    if (IsVisible)
                        _refreshTimer.Start();
                }
            }
            else
            {
                _serverOnline = false;
                _refreshTimer.Stop();
                DisableQueueControls();
            }
        }
        finally
        {
            _checkingServer = false;
        }
    }

    private void LoadUserInformation()
    {
        var user = SessionManager.CurrentUser;
        if (user is null)
        {
            return;
        }

        SidebarNameText.Text = user.FullName;
        SidebarUsernameText.Text = $"@{user.Username}";
    }

    private async Task LoadCurrentShiftAsync()
    {
        try
        {
            var result = await _staffService.GetCurrentShiftAsync();
            if (!result.HasActiveShift || result.Shift is null)
            {
                SessionManager.CurrentShift = null;
                new SelectWindowWindow().Show();
                Close();
                return;
            }

            _shift = result.Shift;
            SessionManager.CurrentShift = _shift;
            DisplayShift();
            await RefreshQueueAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Unable to load shift",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void DisplayShift()
    {
        if (_shift is null)
        {
            return;
        }

        var department = _shift.DisplayDepartment;
        DepartmentText.Text = $"{department.ToUpperInvariant()} • WINDOW {_shift.WindowNumber:00}";
        WindowText.Text = $"Serving at {_shift.WindowName}";
        ShiftStatusText.Text = "ACTIVE";
        ShiftDepartmentText.Text = department;
        ShiftWindowText.Text = _shift.WindowName;
        ShiftStartedText.Text = _shift.StartedAt is null
            ? "Started: ---"
            : $"Started: {_shift.StartedAt.Value:h:mm tt}";
        EndShiftButton.IsEnabled = _serverOnline;
    }

    private async Task RefreshQueueAsync()
    {
        if (_isRefreshing || _shift is null)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            var status = await _queueService.GetDashboardAsync();
            _currentQueue = status.Current;

            WaitingText.Text = status.WaitingCount.ToString();
            NextQueueText.Text = status.Next?.QueueNumber ?? "---";
            CurrentQueueText.Text = _currentQueue?.QueueNumber ?? "---";
            CurrentWindowText.Text = _currentQueue is null
                ? "NO ACTIVE CUSTOMER"
                : $"{_shift.DisplayDepartment.ToUpperInvariant()} • WINDOW {_shift.WindowNumber:00}";

            NextButton.IsEnabled = _serverOnline && _currentQueue is null && status.WaitingCount > 0;
            RecallButton.IsEnabled = _serverOnline && _currentQueue is not null;
            CompleteButton.IsEnabled = _serverOnline && _currentQueue is not null;
            var canNoShow = _currentQueue?.CanNoShow == true;
            NoShowButton.IsEnabled = _serverOnline && _currentQueue is not null && canNoShow;
            NoShowButton.Content = "NO SHOW";
            GraceStatusText.Visibility = _currentQueue is null
                ? Visibility.Collapsed
                : Visibility.Visible;
            GraceStatusText.Text = _currentQueue is null
                ? string.Empty
                : canNoShow
                    ? "GRACE PERIOD ENDED - NO SHOW IS AVAILABLE"
                    : $"NO SHOW AVAILABLE IN {FormatGraceTime(_currentQueue.GraceRemainingSeconds)}";
            CurrentWindowText.Text = _currentQueue?.CustomerAcknowledged == true
                ? $"{_shift.DisplayDepartment.ToUpperInvariant()} • WINDOW {_shift.WindowNumber:00} • CUSTOMER ACKNOWLEDGED"
                : CurrentWindowText.Text;
        }
        catch (Exception ex)
        {
            WindowText.Text = ex.Message;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private static string FormatGraceTime(int seconds) =>
        $"{Math.Max(0, seconds) / 60:00}:{Math.Max(0, seconds) % 60:00}";

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        await RunQueueActionAsync(_queueService.NextAsync);
    }

    private async void RecallButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentQueue is not null)
        {
            await RunQueueActionAsync(() => _queueService.RecallAsync(_currentQueue.QueueId));
        }
    }

    private async void CompleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentQueue is not null)
        {
            await RunQueueActionAsync(() => _queueService.CompleteAsync(_currentQueue.QueueId));
        }
    }

    private async void NoShowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentQueue is not null)
        {
            await RunQueueActionAsync(() => _queueService.NoShowAsync(_currentQueue.QueueId));
        }
    }

    private async Task RunQueueActionAsync(Func<Task> action)
    {
        SetQueueActionsEnabled(false);
        try
        {
            await action();
            await RefreshQueueAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Queue action",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            await RefreshQueueAsync();
        }
    }

    private void SetQueueActionsEnabled(bool enabled)
    {
        NextButton.IsEnabled = enabled && _serverOnline;
        RecallButton.IsEnabled = enabled && _serverOnline;
        CompleteButton.IsEnabled = enabled && _serverOnline;
        NoShowButton.IsEnabled = enabled && _serverOnline;
    }

    private void DisableQueueControls()
    {
        NextButton.IsEnabled = false;
        RecallButton.IsEnabled = false;
        CompleteButton.IsEnabled = false;
        NoShowButton.IsEnabled = false;
        EndShiftButton.IsEnabled = false;
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "Are you sure you want to logout?",
                "PCDS Queue",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        SessionManager.Clear();
        ApiService.ClearToken();
        new LoginWindow().Show();
        Close();
    }

    private async void QueueHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        DashboardContent.Visibility = Visibility.Collapsed;
        HistoryContent.Visibility = Visibility.Visible;

        try
        {
            var result = await _queueService.GetHistoryAsync();
            HistoryDataGrid.ItemsSource = result.History;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Queue History",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void DashboardNavButton_Click(object sender, RoutedEventArgs e)
    {
        HistoryContent.Visibility = Visibility.Collapsed;
        DashboardContent.Visibility = Visibility.Visible;
        await RefreshQueueAsync();
    }

    private async void EndShiftButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_serverOnline)
            return;

        if (MessageBox.Show(
                "End your current shift and release this window?",
                "PCDS Queue",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        EndShiftButton.IsEnabled = false;
        try
        {
            await _staffService.EndShiftAsync();
            SessionManager.CurrentShift = null;
            new SelectWindowWindow().Show();
            Close();
        }
        catch (Exception ex)
        {
            EndShiftButton.IsEnabled = _serverOnline;
            MessageBox.Show(
                ex.Message,
                "PCDS Queue",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _serverTimer.Stop();
        _refreshTimer.Stop();
        base.OnClosed(e);
    }
}
