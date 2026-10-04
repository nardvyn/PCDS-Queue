using System.Windows;
using System.Windows.Controls;
using PCDSQueue.Admin.Models;
using PCDSQueue.Admin.Services;

namespace PCDSQueue.Admin.Views;

public partial class StaffAccountFormWindow : Window
{
    private readonly StaffAccountService _staffService = new();
    private readonly List<DepartmentModel> _departments;
    private readonly StaffAccountModel? _staff;
    private readonly bool _resetPasswordOnly;

    public StaffAccountFormWindow(
        List<DepartmentModel> departments,
        StaffAccountModel? staff = null,
        bool resetPasswordOnly = false)
    {
        InitializeComponent();
        _departments = departments.Where(department => department.IsActive).ToList();
        _staff = staff;
        _resetPasswordOnly = resetPasswordOnly;
        DepartmentComboBox.ItemsSource = _departments;

        if (_resetPasswordOnly)
        {
            Title = "Reset Staff Password";
            TitleText.Text = "RESET PASSWORD";
            SubtitleText.Text = $"Set a new password for {staff?.Username ?? "this account"}";
            FullNameField.Visibility = Visibility.Collapsed;
            UsernameField.Visibility = Visibility.Collapsed;
            DepartmentField.Visibility = Visibility.Collapsed;
            AccountStatusField.Visibility = Visibility.Collapsed;
            PasswordLabel.Text = "NEW PASSWORD (AT LEAST 8 CHARACTERS)";
            SaveButton.Content = "RESET PASSWORD";
            return;
        }

        if (_staff is null)
        {
            DepartmentComboBox.SelectedIndex = _departments.Count > 0 ? 0 : -1;
            return;
        }

        Title = "Edit Staff Account";
        TitleText.Text = "EDIT STAFF ACCOUNT";
        SubtitleText.Text = "Update account details and department assignment";
        SaveButton.Content = "SAVE CHANGES";
        PasswordField.Visibility = Visibility.Collapsed;
        AccountStatusField.Visibility = Visibility.Collapsed;
        FullNameTextBox.Text = _staff.FullName;
        UsernameTextBox.Text = _staff.Username;
        DepartmentComboBox.SelectedItem = _departments.FirstOrDefault(
            department => department.DepartmentId == _staff.DepartmentId)
            ?? _departments.FirstOrDefault();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_resetPasswordOnly)
        {
            await ResetPasswordAsync();
            return;
        }

        var fullName = FullNameTextBox.Text.Trim();
        var username = UsernameTextBox.Text.Trim();
        var department = DepartmentComboBox.SelectedItem as DepartmentModel;
        var password = PasswordInput.Password;
        var status = (AccountStatusComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ACTIVE";

        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 150)
        {
            ShowError("Enter a full name up to 150 characters.");
            return;
        }
        if (string.IsNullOrWhiteSpace(username) || username.Length > 100)
        {
            ShowError("Enter a username up to 100 characters.");
            return;
        }
        if (department is null)
        {
            ShowError("Select an active department.");
            return;
        }
        if (_staff is null && password.Length < 8)
        {
            ShowError("Password must be at least 8 characters.");
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            var request = new StaffAccountSaveRequest
            {
                FullName = fullName,
                Username = username,
                DepartmentId = department.DepartmentId,
                AccountStatus = status,
                Password = _staff is null ? password : null
            };

            if (_staff is null)
                await _staffService.CreateStaffAsync(request);
            else
                await _staffService.UpdateStaffAsync(_staff.StaffId, request);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            SaveButton.IsEnabled = true;
        }
    }

    private async Task ResetPasswordAsync()
    {
        var password = PasswordInput.Password;
        if (password.Length < 8)
        {
            ShowError("Password must be at least 8 characters.");
            return;
        }

        if (_staff is null)
            return;

        SaveButton.IsEnabled = false;
        try
        {
            await _staffService.ResetPasswordAsync(_staff.StaffId, password);
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