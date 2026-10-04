using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using PCDSQueue.Kiosk.Models;
using PCDSQueue.Kiosk.Services;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;

namespace PCDSQueue.Kiosk;

public partial class MainWindow : Window
{
    private readonly DepartmentService _departmentService = new();
    private readonly DispatcherTimer _serverTimer = new()
    {
        Interval = TimeSpan.FromSeconds(3)
    };
    private bool _serverOnline;
    private bool _checkingServer;
    private bool _departmentsLoaded;
    private bool _loadingDepartments;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        _serverTimer.Tick += ServerTimer_Tick;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await CheckServerAsync();
        _serverTimer.Start();
    }

    private async void ServerTimer_Tick(object? sender, EventArgs e) => await CheckServerAsync();

    private async Task CheckServerAsync()
    {
        if (_checkingServer)
            return;

        _checkingServer = true;
        try
        {
            var wasOnline = _serverOnline;
            _serverOnline = await ApiService.IsServerOnlineAsync();
            var statusColor = _serverOnline
                ? Color.FromRgb(22, 163, 106)
                : Color.FromRgb(220, 53, 69);

            ServerStatusDot.Fill = new SolidColorBrush(statusColor);
            ServerStatusText.Text = _serverOnline ? "Server Online" : "Server Offline";
            ServerStatusText.Foreground = new SolidColorBrush(statusColor);
            DepartmentsItemsControl.IsEnabled = _serverOnline;

            if (_serverOnline && (!wasOnline || !_departmentsLoaded))
                await LoadDepartmentsAsync();
            else if (!_serverOnline)
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                DepartmentsItemsControl.Visibility = Visibility.Collapsed;
                ErrorPanel.Visibility = Visibility.Visible;
                ErrorText.Text = "PCDS Queue Server is offline. Please try again shortly.";
            }
        }
        finally
        {
            _checkingServer = false;
        }
    }

    private async Task LoadDepartmentsAsync()
    {
        if (_loadingDepartments)
            return;

        _loadingDepartments = true;
        LoadingPanel.Visibility = Visibility.Visible;
        ErrorPanel.Visibility = Visibility.Collapsed;
        DepartmentsItemsControl.Visibility = Visibility.Collapsed;
        try
        {
            var departments = await _departmentService.GetDepartmentsAsync();
            DepartmentsItemsControl.ItemsSource = departments;
            DepartmentsItemsControl.Visibility = Visibility.Visible;
            DepartmentsItemsControl.IsEnabled = _serverOnline;
            LoadingPanel.Visibility = Visibility.Collapsed;
            _departmentsLoaded = true;
        }
        catch (Exception ex)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            DepartmentsItemsControl.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
            ErrorText.Text = ex.Message;
            _departmentsLoaded = false;
            await UpdateServerStatusAsync();
        }
        finally
        {
            _loadingDepartments = false;
        }
    }

    private async Task UpdateServerStatusAsync()
    {
        _serverOnline = await ApiService.IsServerOnlineAsync();
        var statusColor = _serverOnline
            ? Color.FromRgb(22, 163, 106)
            : Color.FromRgb(220, 53, 69);
        ServerStatusDot.Fill = new SolidColorBrush(statusColor);
        ServerStatusText.Text = _serverOnline ? "Server Online" : "Server Offline";
        ServerStatusText.Foreground = new SolidColorBrush(statusColor);
        DepartmentsItemsControl.IsEnabled = _serverOnline;
    }

    private async void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        await CheckServerAsync();
        if (_serverOnline && !_departmentsLoaded)
            await LoadDepartmentsAsync();
    }

    private void DepartmentButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: DepartmentModel department })
            return;
        if (!_serverOnline || !department.IsAvailableForKiosk)
            return;

        try
        {
            new Views.DepartmentQueueWindow(department).Show();
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Unable to Open Department",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _serverTimer.Stop();
        base.OnClosed(e);
    }
}
