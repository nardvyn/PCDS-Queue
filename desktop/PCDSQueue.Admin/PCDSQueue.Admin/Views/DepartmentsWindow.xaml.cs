using System.Windows;
using System.Windows.Controls;
using PCDSQueue.Admin.Models;
using PCDSQueue.Admin.Services;

namespace PCDSQueue.Admin.Views;

public partial class DepartmentsWindow : UserControl
{
    private readonly DepartmentService _departmentService = new();
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
            DepartmentsGrid.ItemsSource = await _departmentService.GetDepartmentsAsync();
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

    private async void ToggleStatusButton_Click(object sender, RoutedEventArgs e)
    {
        if (DepartmentsGrid.SelectedItem is not DepartmentModel department)
            return;

        ToggleStatusButton.IsEnabled = false;
        try
        {
            await _departmentService.SetDepartmentStatusAsync(
                department.DepartmentId,
                !department.IsActive);
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
        ToggleStatusButton.IsEnabled = !_isLoading && selected is not null;
        ToggleStatusButton.Content = selected?.IsActive == true ? "DEACTIVATE" : "ACTIVATE";
    }

}