using System.Windows;
using System.Windows.Controls;
using PCDSQueue.Admin.Models;
using PCDSQueue.Admin.Services;

namespace PCDSQueue.Admin.Views;

public partial class StaffAccountsWindow : UserControl
{
    private readonly DepartmentService _departmentService = new();
    private readonly StaffAccountService _staffService = new();
    private List<DepartmentModel> _departments = [];
    private List<StaffAccountModel> _staffAccounts = [];
    private bool _loadingFilters;
    private bool _isLoading;

    public StaffAccountsWindow()
    {
        InitializeComponent();
    }

    public async Task RefreshAsync()
    {
        _isLoading = true;
        try
        {
            _departments = await _departmentService.GetDepartmentsAsync();
            _staffAccounts = (await _staffService.GetStaffAsync())
                .Where(staff => !staff.Username.Contains(
                    "[DELETED:",
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            LoadDepartmentFilter();
            ApplyFilters();
            StaffErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            _isLoading = false;
            UpdateActions();
        }
    }

    private void LoadDepartmentFilter()
    {
        var previousId = (DepartmentFilterComboBox.SelectedItem as DepartmentFilterOption)?.DepartmentId;
        _loadingFilters = true;
        var options = new List<DepartmentFilterOption>
        {
            new("All departments", null)
        };
        options.AddRange(_departments.Select(department =>
            new DepartmentFilterOption(department.DepartmentName, department.DepartmentId)));
        DepartmentFilterComboBox.ItemsSource = options;
        DepartmentFilterComboBox.SelectedItem = options.FirstOrDefault(option => option.DepartmentId == previousId)
            ?? options[0];
        StatusFilterComboBox.SelectedIndex = Math.Clamp(StatusFilterComboBox.SelectedIndex, 0, 3);
        _loadingFilters = false;
    }

    private void ApplyFilters()
    {
        var query = SearchTextBox.Text.Trim();
        var departmentId = (DepartmentFilterComboBox.SelectedItem as DepartmentFilterOption)?.DepartmentId;
        var status = (StatusFilterComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();

        var filtered = _staffAccounts.Where(staff =>
            (string.IsNullOrWhiteSpace(query)
                || staff.FullName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || staff.Username.Contains(query, StringComparison.OrdinalIgnoreCase))
            && (departmentId is null || staff.DepartmentId == departmentId)
            && (string.IsNullOrWhiteSpace(status) || status == "ALL" || staff.AccountStatus == status));

        StaffGrid.ItemsSource = filtered.ToList();
        UpdateActions();
    }

    private async void AddStaffButton_Click(object sender, RoutedEventArgs e)
    {
        if (_departments.All(department => !department.IsActive))
        {
            ShowError("Create or activate a department before adding staff.");
            return;
        }

        var form = new StaffAccountFormWindow(_departments) { Owner = Window.GetWindow(this) };
        if (form.ShowDialog() == true)
            await RefreshAsync();
    }

    private async void EditStaffButton_Click(object sender, RoutedEventArgs e)
    {
        if (StaffGrid.SelectedItem is not StaffAccountModel staff)
            return;

        var form = new StaffAccountFormWindow(_departments, staff) { Owner = Window.GetWindow(this) };
        if (form.ShowDialog() == true)
            await RefreshAsync();
    }

    private async void ResetPasswordButton_Click(object sender, RoutedEventArgs e)
    {
        if (StaffGrid.SelectedItem is not StaffAccountModel staff)
            return;

        var form = new StaffAccountFormWindow(_departments, staff, resetPasswordOnly: true)
        {
            Owner = Window.GetWindow(this)
        };
        if (form.ShowDialog() == true)
            await RefreshAsync();
    }

    private async void ArchiveStaffButton_Click(object sender, RoutedEventArgs e)
    {
        if (StaffGrid.SelectedItem is not StaffAccountModel staff)
            return;

        var archive = staff.AccountStatus == "ACTIVE";
        if (archive && MessageBox.Show(
                $"Archive {staff.FullName}'s account?\n\nThe staff member will lose access, but shift and queue history will be kept. You can restore the account later.",
                "Archive staff account",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }

        ArchiveStaffButton.IsEnabled = false;
        try
        {
            var status = archive ? "INACTIVE" : "ACTIVE";
            await _staffService.SetStaffStatusAsync(staff.StaffId, status);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            UpdateActions();
        }
    }

    private async void DeleteStaffButton_Click(object sender, RoutedEventArgs e)
    {
        if (StaffGrid.SelectedItem is not StaffAccountModel staff)
            return;

        if (MessageBox.Show(
                $"Delete {staff.FullName}'s account?\n\nThe account will be deactivated and unassigned, and its username will be released. Queue and shift history will be retained.",
                "Delete staff account",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }

        DeleteStaffButton.IsEnabled = false;
        try
        {
            await _staffService.DeleteStaffAsync(staff.StaffId);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            UpdateActions();
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loadingFilters)
            ApplyFilters();
    }

    private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loadingFilters)
            ApplyFilters();
    }

    private void StaffGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateActions();

    private void UpdateActions()
    {
        var selected = StaffGrid.SelectedItem as StaffAccountModel;
        var hasSelection = !_isLoading && selected is not null;
        EditStaffButton.IsEnabled = hasSelection;
        ResetPasswordButton.IsEnabled = hasSelection;
        ArchiveStaffButton.IsEnabled = hasSelection;
        ArchiveStaffButton.Content = selected?.AccountStatus == "ACTIVE" ? "ARCHIVE" : "RESTORE";
        DeleteStaffButton.IsEnabled = hasSelection;
        AddStaffButton.IsEnabled = !_isLoading && _departments.Any(department => department.IsActive);
    }

    private void ShowError(string message)
    {
        StaffErrorText.Text = message;
        StaffErrorText.Visibility = Visibility.Visible;
    }

    private sealed record DepartmentFilterOption(string Name, int? DepartmentId)
    {
        public string DepartmentName => Name;
    }
}