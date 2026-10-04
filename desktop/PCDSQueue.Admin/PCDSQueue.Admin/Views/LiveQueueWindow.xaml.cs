using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PCDSQueue.Admin.Models;
using PCDSQueue.Admin.Services;

namespace PCDSQueue.Admin.Views;

public partial class LiveQueueWindow : UserControl
{
    private readonly DepartmentService _departmentService = new();
    private readonly LiveQueueService _liveQueueService = new();
    private readonly DispatcherTimer _refreshTimer = new()
    {
        Interval = TimeSpan.FromSeconds(3)
    };
    private bool _loadingDepartments;
    private bool _refreshing;

    public LiveQueueWindow()
    {
        InitializeComponent();
        _refreshTimer.Tick += RefreshTimer_Tick;
    }

    public async Task RefreshAsync()
    {
        await LoadDepartmentsAsync();
        await RefreshSnapshotAsync();
        _refreshTimer.Start();
    }

    public void StopPolling() => _refreshTimer.Stop();

    private async Task LoadDepartmentsAsync()
    {
        var selectedId = (DepartmentFilterComboBox.SelectedItem as DepartmentFilterOption)?.DepartmentId;
        _loadingDepartments = true;
        try
        {
            var departments = await _departmentService.GetDepartmentsAsync();
            var options = new List<DepartmentFilterOption>
            {
                new("All departments", null)
            };
            options.AddRange(departments
                .Where(department => department.IsActive)
                .Select(department => new DepartmentFilterOption(
                    department.DepartmentName,
                    department.DepartmentId)));
            DepartmentFilterComboBox.ItemsSource = options;
            DepartmentFilterComboBox.SelectedItem = options.FirstOrDefault(
                option => option.DepartmentId == selectedId) ?? options[0];
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            _loadingDepartments = false;
        }
    }

    private async void DepartmentFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loadingDepartments)
            await RefreshSnapshotAsync();
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e) => await RefreshSnapshotAsync();

    private async Task RefreshSnapshotAsync()
    {
        if (_refreshing)
            return;

        _refreshing = true;
        LiveStateText.Text = "Updating...";
        try
        {
            var departmentId = (DepartmentFilterComboBox.SelectedItem as DepartmentFilterOption)?.DepartmentId;
            var snapshot = await _liveQueueService.GetSnapshotAsync(departmentId);
            WaitingCountText.Text = snapshot.Summary.Waiting.ToString("N0");
            ServingCountText.Text = snapshot.Summary.Serving.ToString("N0");
            WindowsInUseText.Text = snapshot.Summary.WindowsInUse.ToString("N0");
            ServingGrid.ItemsSource = snapshot.Serving;
            WaitingGrid.ItemsSource = snapshot.Waiting;
            LiveDot.Fill = new SolidColorBrush(Color.FromRgb(22, 163, 106));
            LiveStateText.Text = "Live";
            LiveStateText.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 106));
            LastUpdatedText.Text = $"Updated {DateTime.Now:h:mm:ss tt}";
            LiveErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            LiveDot.Fill = new SolidColorBrush(Color.FromRgb(220, 53, 69));
            LiveStateText.Text = "Connection issue";
            LiveStateText.Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69));
            ShowError(ex.Message);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void ShowError(string message)
    {
        LiveErrorText.Text = message;
        LiveErrorText.Visibility = Visibility.Visible;
    }

    private sealed record DepartmentFilterOption(string DepartmentName, int? DepartmentId);
}