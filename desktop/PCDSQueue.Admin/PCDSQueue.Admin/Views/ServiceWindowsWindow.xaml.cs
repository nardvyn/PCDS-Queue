using System.Windows;
using System.Windows.Controls;
using PCDSQueue.Admin.Models;
using PCDSQueue.Admin.Services;

namespace PCDSQueue.Admin.Views;

public partial class ServiceWindowsWindow : UserControl
{
    private readonly DepartmentService _departmentService = new();
    private readonly ServiceWindowService _windowService = new();
    private bool _loadingDepartments;
    private bool _loadingWindows;

    public ServiceWindowsWindow()
    {
        InitializeComponent();
    }

    public Task RefreshAsync() => LoadDepartmentsAsync();

    private async Task LoadDepartmentsAsync()
    {
        var previousDepartmentId = (DepartmentComboBox.SelectedItem as DepartmentModel)?.DepartmentId;
        _loadingDepartments = true;
        try
        {
            var departments = await _departmentService.GetDepartmentsAsync();
            var activeDepartments = departments.Where(department => department.IsActive).ToList();
            DepartmentComboBox.ItemsSource = activeDepartments;
            DepartmentComboBox.SelectedItem = activeDepartments.FirstOrDefault(
                department => department.DepartmentId == previousDepartmentId)
                ?? activeDepartments.FirstOrDefault();
            AddWindowButton.IsEnabled = DepartmentComboBox.SelectedItem is not null;
            if (DepartmentComboBox.SelectedItem is null)
                WindowsGrid.ItemsSource = null;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            _loadingDepartments = false;
        }

        await LoadWindowsAsync();
    }

    private async Task LoadWindowsAsync()
    {
        if (_loadingWindows)
            return;

        var department = DepartmentComboBox.SelectedItem as DepartmentModel;
        if (department is null)
        {
            WindowsGrid.ItemsSource = null;
            UpdateActions();
            return;
        }

        _loadingWindows = true;
        try
        {
            WindowsGrid.ItemsSource = await _windowService.GetWindowsAsync(department.DepartmentId);
            WindowsErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            _loadingWindows = false;
            UpdateActions();
        }
    }

    private async void DepartmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loadingDepartments)
        {
            AddWindowButton.IsEnabled = DepartmentComboBox.SelectedItem is not null;
            await LoadWindowsAsync();
        }
    }

    private async void AddWindowButton_Click(object sender, RoutedEventArgs e)
    {
        if (DepartmentComboBox.SelectedItem is not DepartmentModel department)
            return;

        var form = new ServiceWindowFormWindow(department) { Owner = Window.GetWindow(this) };
        if (form.ShowDialog() == true)
            await LoadWindowsAsync();
    }

    private async void EditWindowButton_Click(object sender, RoutedEventArgs e)
    {
        if (DepartmentComboBox.SelectedItem is not DepartmentModel department ||
            WindowsGrid.SelectedItem is not ServiceWindowModel window)
            return;

        var form = new ServiceWindowFormWindow(department, window) { Owner = Window.GetWindow(this) };
        if (form.ShowDialog() == true)
            await LoadWindowsAsync();
    }

    private async void ToggleWindowStatusButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowsGrid.SelectedItem is not ServiceWindowModel window)
            return;

        ToggleWindowStatusButton.IsEnabled = false;
        try
        {
            await _windowService.SetWindowStatusAsync(window.WindowId, !window.IsActive);
            await LoadWindowsAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            UpdateActions();
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadWindowsAsync();

    private void WindowsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateActions();

    private void UpdateActions()
    {
        var selected = WindowsGrid.SelectedItem as ServiceWindowModel;
        EditWindowButton.IsEnabled = !_loadingWindows && selected is not null;
        ToggleWindowStatusButton.IsEnabled = !_loadingWindows && selected is not null;
        ToggleWindowStatusButton.Content = selected?.IsActive == true ? "DEACTIVATE" : "ACTIVATE";
    }

    private void ShowError(string message)
    {
        WindowsErrorText.Text = message;
        WindowsErrorText.Visibility = Visibility.Visible;
    }
}