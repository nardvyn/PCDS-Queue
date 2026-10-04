using System.Windows;
using PCDSQueue.Admin.Models;
using PCDSQueue.Admin.Services;

namespace PCDSQueue.Admin.Views;

public partial class ServiceWindowFormWindow : Window
{
    private readonly ServiceWindowService _service = new();
    private readonly int _departmentId;
    private readonly ServiceWindowModel? _window;

    public ServiceWindowFormWindow(DepartmentModel department, ServiceWindowModel? window = null)
    {
        InitializeComponent();
        _departmentId = department.DepartmentId;
        _window = window;
        DepartmentText.Text = department.DepartmentName;

        if (_window is null)
            return;

        Title = "Edit Service Window";
        TitleText.Text = "EDIT SERVICE WINDOW";
        SaveButton.Content = "SAVE CHANGES";
        WindowNameTextBox.Text = _window.WindowName;
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var windowName = WindowNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(windowName) || windowName.Length > 100)
        {
            ShowError("Enter a window name up to 100 characters.");
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            if (_window is null)
                    await _service.CreateWindowAsync(_departmentId, windowName);
            else
                await _service.UpdateWindowAsync(_window.WindowId, windowName);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            SaveButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}