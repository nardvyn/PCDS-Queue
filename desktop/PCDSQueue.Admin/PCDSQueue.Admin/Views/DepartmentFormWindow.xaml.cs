using System.Windows;
using System.Windows.Controls;
using PCDSQueue.Admin.Models;
using PCDSQueue.Admin.Services;

namespace PCDSQueue.Admin.Views;

public partial class DepartmentFormWindow : Window
{
    private readonly DepartmentService _departmentService = new();
    private readonly DepartmentModel? _department;

    public DepartmentFormWindow(DepartmentModel? department = null)
    {
        InitializeComponent();
        _department = department;

        if (_department is null)
            return;

        Title = "Edit Department";
        TitleText.Text = "EDIT DEPARTMENT";
        SaveButton.Content = "SAVE CHANGES";
        DepartmentNameTextBox.Text = _department.DepartmentName;
        QueuePrefixTextBox.Text = _department.QueuePrefix;
        WindowCountTextBox.Text = _department.WindowCount.ToString();
        StatusComboBox.SelectedIndex = _department.IsActive ? 0 : 1;
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var name = DepartmentNameTextBox.Text.Trim();
        var prefix = QueuePrefixTextBox.Text.Trim().ToUpperInvariant();
        var isActive = (StatusComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() == "Active";

        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            ShowError("Enter a department name up to 100 characters.");
            return;
        }

        if (string.IsNullOrWhiteSpace(prefix) || prefix.Length > 10)
        {
            ShowError("Enter a queue prefix up to 10 characters.");
            return;
        }

        if (!int.TryParse(WindowCountTextBox.Text, out var windowCount) || windowCount is < 1 or > 50)
        {
            ShowError("Enter a window count from 1 to 50.");
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            var request = new DepartmentSaveRequest
            {
                DepartmentName = name,
                QueuePrefix = prefix,
                WindowCount = windowCount,
                IsActive = isActive
            };

            if (_department is null)
                await _departmentService.CreateDepartmentAsync(request);
            else
                await _departmentService.UpdateDepartmentAsync(_department.DepartmentId, request);

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