using System.Windows;
using System.Windows.Controls;
using PCDSQueue.Admin.Models;
using PCDSQueue.Admin.Services;

namespace PCDSQueue.Admin.Views;

public partial class DepartmentsWindow : UserControl
{
    private readonly DepartmentService _departmentService = new();
    private List<DepartmentModel> _departments = [];
    private bool _isLoading;

    public DepartmentsWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadDepartmentsAsync();
    }

    public Task RefreshAsync() => LoadDepartmentsAsync();

    private async Task LoadDepartmentsAsync()
    {
        _isLoading = true;
        try
        {
            _departments = (await _departmentService.GetDepartmentsAsync())
                .Where(department => !department.DepartmentName.Contains(
                    " [DELETED:",
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            DepartmentsGrid.ItemsSource = _departments;
            DepartmentsErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            DepartmentsErrorText.Text = ex.Message;
            DepartmentsErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            _isLoading = false;
            UpdateSelectionActions();
        }
    }

    private async void AddDepartmentButton_Click(object sender, RoutedEventArgs e)
    {
        var form = new DepartmentFormWindow { Owner = Window.GetWindow(this) };
        if (form.ShowDialog() == true)
            await LoadDepartmentsAsync();
    }

    private async void EditDepartmentButton_Click(object sender, RoutedEventArgs e)
    {
        if (DepartmentsGrid.SelectedItem is not DepartmentModel department)
            return;

        var form = new DepartmentFormWindow(department) { Owner = Window.GetWindow(this) };
        if (form.ShowDialog() == true)
            await LoadDepartmentsAsync();
    }

    private async void ArchiveDepartmentButton_Click(object sender, RoutedEventArgs e)
    {
        if (DepartmentsGrid.SelectedItem is not DepartmentModel department)
            return;

        var archive = department.IsActive;
        if (archive && MessageBox.Show(
                $"Archive {department.DepartmentName}?\n\nIt will be removed from active queue operations, but its queue history will be kept. You can restore it later.",
                "Archive department",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }

        ArchiveDepartmentButton.IsEnabled = false;
        try
        {
            await _departmentService.SetDepartmentStatusAsync(
                department.DepartmentId,
                !archive);
            await LoadDepartmentsAsync();
        }
        catch (Exception ex)
        {
            DepartmentsErrorText.Text = ex.Message;
            DepartmentsErrorText.Visibility = Visibility.Visible;
            UpdateSelectionActions();
        }
    }

    private async void DeleteDepartmentButton_Click(object sender, RoutedEventArgs e)
    {
        if (DepartmentsGrid.SelectedItem is not DepartmentModel department)
            return;

        if (MessageBox.Show(
                $"Delete {department.DepartmentName}?\n\nThis can be done directly without archiving first. Its queue history will be kept under a deleted label, and its name and prefix will be released.",
                "Delete department",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }

        DeleteDepartmentButton.IsEnabled = false;
        try
        {
            await _departmentService.DeleteDepartmentAsync(department.DepartmentId);
            await LoadDepartmentsAsync();
        }
        catch (Exception ex)
        {
            DepartmentsErrorText.Text = ex.Message;
            DepartmentsErrorText.Visibility = Visibility.Visible;
            UpdateSelectionActions();
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadDepartmentsAsync();

    private void DepartmentsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSelectionActions();

    private void UpdateSelectionActions()
    {
        var selected = DepartmentsGrid.SelectedItem as DepartmentModel;
        EditDepartmentButton.IsEnabled = !_isLoading && selected is not null;
        ArchiveDepartmentButton.IsEnabled = !_isLoading && selected is not null;
        ArchiveDepartmentButton.Content = selected?.IsActive == true ? "ARCHIVE" : "RESTORE";
        DeleteDepartmentButton.IsEnabled = !_isLoading && selected is not null;
    }

}