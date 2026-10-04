using System.Windows;
using System.Windows.Controls;
using PCDSQueue.Admin.Models;
using PCDSQueue.Admin.Services;

namespace PCDSQueue.Admin.Views;

public partial class ReportsWindow : UserControl
{
    private readonly DepartmentService _departmentService = new();
    private readonly ReportService _reportService = new();

    public ReportsWindow()
    {
        InitializeComponent();
        DateFromPicker.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DateToPicker.SelectedDate = DateTime.Today;
    }

    public async Task RefreshAsync()
    {
        await LoadDepartmentsAsync();
        await GenerateReportAsync();
    }

    private async Task LoadDepartmentsAsync()
    {
        var selectedId = (DepartmentFilterComboBox.SelectedItem as DepartmentFilterOption)?.DepartmentId;
        try
        {
            var departments = await _departmentService.GetDepartmentsAsync();
            var options = new List<DepartmentFilterOption>
            {
                new("All departments", null)
            };
            options.AddRange(departments.Select(department =>
                new DepartmentFilterOption(department.DepartmentName, department.DepartmentId)));
            DepartmentFilterComboBox.ItemsSource = options;
            DepartmentFilterComboBox.SelectedItem = options.FirstOrDefault(
                option => option.DepartmentId == selectedId) ?? options[0];
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async void GenerateButton_Click(object sender, RoutedEventArgs e) => await GenerateReportAsync();

    private async Task GenerateReportAsync()
    {
        var dateFrom = DateFromPicker.SelectedDate;
        var dateTo = DateToPicker.SelectedDate;
        if (dateFrom is null || dateTo is null)
        {
            ShowError("Select both a From and To date.");
            return;
        }
        if (dateFrom.Value.Date > dateTo.Value.Date)
        {
            ShowError("The From date must be on or before the To date.");
            return;
        }

        var departmentId = (DepartmentFilterComboBox.SelectedItem as DepartmentFilterOption)?.DepartmentId;
        GenerateButton.IsEnabled = false;
        try
        {
            var report = await _reportService.GetReportAsync(
                dateFrom.Value.Date,
                dateTo.Value.Date,
                departmentId);

            TotalQueuesText.Text = report.Summary.TotalQueues.ToString("N0");
            CompletedText.Text = report.Summary.Completed.ToString("N0");
            NoShowText.Text = report.Summary.NoShow.ToString("N0");
            AverageWaitingText.Text = ReportTimeFormatter.Format(report.Summary.AverageWaitingSeconds);
            AverageServiceText.Text = ReportTimeFormatter.Format(report.Summary.AverageServiceSeconds);
            DepartmentPerformanceGrid.ItemsSource = report.Departments;
            ReportErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            GenerateButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ReportErrorText.Text = message;
        ReportErrorText.Visibility = Visibility.Visible;
    }

    private sealed record DepartmentFilterOption(string DepartmentName, int? DepartmentId);
}