using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PCDSQueue.TVDisplay.Models;
using PCDSQueue.TVDisplay.Services;

namespace PCDSQueue.TVDisplay;

public partial class MainWindow : Window
{
    private readonly ApiService _apiService = new();
    private readonly VoiceService _voiceService = new();
    private readonly SoundService _soundService = new();
    private readonly DispatcherTimer _refreshTimer = new()
    {
        Interval = TimeSpan.FromSeconds(3)
    };
    private readonly DispatcherTimer _clockTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };
    private readonly DispatcherTimer _highlightTimer = new()
    {
        Interval = TimeSpan.FromSeconds(5)
    };
    private bool _isLoading;
    private bool _announcementCursorInitialized;
    private bool _voiceAnnouncementsEnabled = true;
    private long _lastAnnouncementEventId;
    private long? _highlightedQueueId;
    private List<DisplayQueueModel> _currentCalls = [];

    public MainWindow()
    {
        InitializeComponent();
        _refreshTimer.Tick += RefreshTimer_Tick;
        _clockTimer.Tick += (_, _) => UpdateClock();
        _highlightTimer.Tick += (_, _) => SetHighlightedQueue(null);
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateClock();
        _clockTimer.Start();
        await LoadDisplayAsync();
        _refreshTimer.Start();
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e) =>
        await LoadDisplayAsync();

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockText.Text = now.ToString("h:mm:ss tt");
        DateText.Text = now.ToString("dddd, MMMM d, yyyy");
    }

    private async Task LoadDisplayAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        try
        {
            var response = await _apiService.GetDisplayAsync();
            _refreshTimer.Interval = TimeSpan.FromSeconds(
                Math.Clamp(response.RefreshInterval, 1, 60));
            var wasVoiceEnabled = _voiceAnnouncementsEnabled;
            _voiceAnnouncementsEnabled = response.TvVoiceEnabled;
            if (wasVoiceEnabled && !_voiceAnnouncementsEnabled)
            {
                _soundService.Stop();
                _voiceService.Cancel();
            }

            var departments = response.Departments;
            var calls = departments
                .SelectMany(department => department.Serving.Select(queue => new DisplayQueueModel
                {
                    QueueId = queue.QueueId,
                    QueueNumber = queue.QueueNumber,
                    DepartmentName = department.DepartmentName,
                    WindowNumber = queue.WindowNumber,
                    WindowName = queue.WindowName,
                    IsAnnouncementHighlighted = queue.QueueId == _highlightedQueueId
                }))
                .ToList();

            _currentCalls = calls;
            ServingItems.ItemsSource = calls;
            WaitingItems.ItemsSource = departments;
            WaitingCountText.Text = departments.Sum(department => department.WaitingCount).ToString();
            CallCountText.Text = $"{calls.Count:00} ACTIVE";
            RefreshMainCall();
            SetServerStatus(true);
            await PollAnnouncementEventsAsync();
        }
        catch
        {
            ServingItems.ItemsSource = null;
            WaitingItems.ItemsSource = null;
            _currentCalls = [];
            MainQueueNumber.Visibility = Visibility.Collapsed;
            MainDepartment.Text = string.Empty;
            MainWindowNumber.Text = string.Empty;
            MainCallEmptyPanel.Visibility = Visibility.Visible;
            EmptyStateHeading.Text = "DISPLAY OFFLINE";
            EmptyStateMessage.Text = "Waiting to reconnect to the queue server.";
            MainCallCard.Tag = false;
            WaitingCountText.Text = "--";
            CallCountText.Text = "-- ACTIVE";
            SetServerStatus(false);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task PollAnnouncementEventsAsync()
    {
        try
        {
            var response = await _apiService.GetAnnouncementEventsAsync(_lastAnnouncementEventId);
            if (!_announcementCursorInitialized)
            {
                _lastAnnouncementEventId = response.LatestEventId;
                _announcementCursorInitialized = true;
                return;
            }

            foreach (var announcement in response.Events)
            {
                if (announcement.EventId <= _lastAnnouncementEventId)
                    continue;

                SetHighlightedQueue(announcement.QueueId);
                if (!_voiceAnnouncementsEnabled)
                    continue;

                _soundService.PlayDing();
                await Task.Delay(800);
                if (_voiceAnnouncementsEnabled)
                    await _voiceService.AnnounceAsync(announcement);
            }

            _lastAnnouncementEventId = Math.Max(
                _lastAnnouncementEventId,
                response.NextAfterEventId);
        }
        catch
        {
            // Keep the visual display online if announcement polling temporarily fails.
        }
    }

    private void SetHighlightedQueue(long? queueId)
    {
        _highlightTimer.Stop();
        _highlightedQueueId = queueId;

        foreach (var queue in _currentCalls)
            queue.IsAnnouncementHighlighted = queue.QueueId == queueId;

        ServingItems.Items.Refresh();
        RefreshMainCall();

        if (queueId is not null)
            _highlightTimer.Start();
    }

    private void RefreshMainCall()
    {
        var mainCall = _currentCalls.FirstOrDefault(
            call => call.QueueId == _highlightedQueueId) ?? _currentCalls.FirstOrDefault();

        if (mainCall is null)
        {
            MainQueueNumber.Text = string.Empty;
            MainQueueNumber.Visibility = Visibility.Collapsed;
            MainDepartment.Text = string.Empty;
            MainWindowNumber.Text = string.Empty;
            ProceedText.Text = "Please wait for your number to be called.";
            MainCallEmptyPanel.Visibility = Visibility.Visible;
            MainCallCard.Tag = false;
            EmptyStateHeading.Text = "NO ACTIVE CALLS";
            EmptyStateMessage.Text = "Please wait for your number to be called.";
            return;
        }

        MainCallEmptyPanel.Visibility = Visibility.Collapsed;
        MainQueueNumber.Visibility = Visibility.Visible;
        MainQueueNumber.Text = mainCall.QueueNumber;
        MainDepartment.Text = mainCall.DepartmentName;
        MainWindowNumber.Text = mainCall.WindowDisplay;
        ProceedText.Text = string.IsNullOrWhiteSpace(mainCall.WindowName)
            ? $"Please proceed to {mainCall.DepartmentName}, {mainCall.WindowDisplay}."
            : $"Please proceed to {mainCall.DepartmentName}, {mainCall.WindowName}.";
        MainCallCard.Tag = mainCall.IsAnnouncementHighlighted;
    }

    private void SetServerStatus(bool online)
    {
        ServerDot.Fill = new SolidColorBrush(online
            ? Color.FromRgb(22, 131, 93)
            : Color.FromRgb(196, 55, 55));
        ServerText.Text = online ? "SERVER ONLINE" : "SERVER OFFLINE";
        LiveDot.Fill = new SolidColorBrush(online
            ? Color.FromRgb(64, 214, 147)
            : Color.FromRgb(255, 141, 141));
        LiveText.Text = online ? "LIVE" : "RECONNECTING";
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        _refreshTimer.Stop();
        _clockTimer.Stop();
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;
        WindowState = WindowState.Normal;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _refreshTimer.Stop();
        _clockTimer.Stop();
        _highlightTimer.Stop();
        _soundService.Dispose();
        _voiceService.Dispose();
    }
}