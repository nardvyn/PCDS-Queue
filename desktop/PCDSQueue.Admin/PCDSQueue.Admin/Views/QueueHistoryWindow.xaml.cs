using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCDSQueue.Admin.Models;
using PCDSQueue.Admin.Services;

namespace PCDSQueue.Admin.Views;

public partial class QueueHistoryWindow : UserControl
{
    private readonly DepartmentService _departmentService = new();
    private readonly QueueHistoryService _historyService = new();
    private bool _isSearching;

    public QueueHistoryWindow()
    {
        InitializeComponent();
        DateFromPicker.SelectedDate = DateTime.Today;
        DateToPicker.SelectedDate = DateTime.Today;
    }

    public async Task RefreshAsync()
    {
        await LoadDepartmentsAsync();
        await SearchHistoryAsync();
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

    private async void SearchButton_Click(object sender, RoutedEventArgs e) => await SearchHistoryAsync();

    private async Task SearchHistoryAsync()
    {
        var dateFrom = DateFromPicker.SelectedDate;
        var dateTo = DateToPicker.SelectedDate;
        if (dateFrom is not null && dateTo is not null && dateFrom.Value.Date > dateTo.Value.Date)
        {
            ShowError("The From date must be on or before the To date.");
            return;
        }

        var departmentId = (DepartmentFilterComboBox.SelectedItem as DepartmentFilterOption)?.DepartmentId;
        var status = (StatusFilterComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var source = (SourceFilterComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();

        _isSearching = true;
        SearchButton.IsEnabled = false;
        try
        {
            HistoryGrid.ItemsSource = await _historyService.SearchAsync(
                dateFrom,
                dateTo,
                departmentId,
                status,
                source);
            HistoryErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            _isSearching = false;
            SearchButton.IsEnabled = true;
            UpdateDetailsButton();
        }
    }

    private async void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        DateFromPicker.SelectedDate = DateTime.Today;
        DateToPicker.SelectedDate = DateTime.Today;
        StatusFilterComboBox.SelectedIndex = 0;
        SourceFilterComboBox.SelectedIndex = 0;
        DepartmentFilterComboBox.SelectedIndex = 0;
        await SearchHistoryAsync();
    }

    private async void ViewDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        await OpenSelectedDetailsAsync();
    }

    private async void HistoryGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryGrid.SelectedItem is not null)
            await OpenSelectedDetailsAsync();
    }

    private async Task OpenSelectedDetailsAsync()
    {
        if (HistoryGrid.SelectedItem is not AdminQueueHistoryItem selected)
            return;

        ViewDetailsButton.IsEnabled = false;
        try
        {
            var details = await _historyService.GetDetailsAsync(selected.QueueId);
            new QueueHistoryDetailsWindow(details)
            {
                Owner = Window.GetWindow(this)
            }.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            UpdateDetailsButton();
        }
    }

    private void HistoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateDetailsButton();

    private void UpdateDetailsButton() =>
        ViewDetailsButton.IsEnabled = !_isSearching && HistoryGrid.SelectedItem is not null;

    private void ShowError(string message)
    {
        HistoryErrorText.Text = message;
        HistoryErrorText.Visibility = Visibility.Visible;
    }

    private sealed record DepartmentFilterOption(string DepartmentName, int? DepartmentId);
}