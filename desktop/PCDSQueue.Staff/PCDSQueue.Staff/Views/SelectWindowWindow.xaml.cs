using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCDSQueue.Staff.Helpers;
using PCDSQueue.Staff.Models;
using PCDSQueue.Staff.Services;

namespace PCDSQueue.Staff.Views;

public partial class SelectWindowWindow : Window
{
    private readonly StaffService _staffService = new();
    private List<WindowModel> _windows = [];
    private WindowModel? _selectedWindow;

    public SelectWindowWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadWindowsAsync();
    }

    private async Task LoadWindowsAsync()
    {
        SetLoading(true);
        HideError();

        try
        {
            var currentShift = await _staffService.GetCurrentShiftAsync();
            if (currentShift.HasActiveShift && currentShift.Shift is not null)
            {
                SessionManager.CurrentShift = currentShift.Shift;
                new StaffDashboardWindow().Show();
                Close();
                return;
            }

            var response = await _staffService.GetWindowsAsync();
            if (response.Department is null)
            {
                throw new Exception("Department information was not returned.");
            }

            DepartmentText.Text = response.Department.DepartmentName.ToUpperInvariant();
            _windows = response.Windows;
            RenderWindows();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void RenderWindows()
    {
        WindowsPanel.Children.Clear();

        foreach (var window in _windows)
        {
            var isSelected = window.WindowId == _selectedWindow?.WindowId;
            var card = new Border
            {
                Width = 300,
                Height = 210,
                Margin = new Thickness(0, 0, 20, 20),
                Padding = new Thickness(25),
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                BorderBrush = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(isSelected ? "#173F8A" : "#E2E8F0")),
                Background = Brushes.White
            };

            var content = new StackPanel();
            content.Children.Add(new TextBlock
            {
                Text = $"WINDOW {window.WindowNumber:00}",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#172033")),
                FontSize = 21,
                FontWeight = FontWeights.Bold
            });

            content.Children.Add(new TextBlock
            {
                Text = window.WindowName,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#718096")),
                FontSize = 12,
                Margin = new Thickness(0, 8, 0, 20)
            });

            var status = window.Available ? "AVAILABLE" : "IN USE";
            content.Children.Add(new TextBlock
            {
                Text = $"●  {status}",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                    window.Available ? "#16A36A" : "#DC3545")),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            });

            content.Children.Add(new TextBlock
            {
                Text = window.Available
                    ? "Ready for your shift"
                    : window.AssignedStaffName ?? "Assigned to another staff member",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#718096")),
                FontSize = 11,
                Margin = new Thickness(0, 8, 0, 15)
            });

            var selectButton = new Button
            {
                Content = isSelected ? "SELECTED" : "SELECT",
                Height = 36,
                IsEnabled = window.Available,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                    isSelected ? "#102E68" : "#173F8A")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            selectButton.Click += (_, _) => SelectWindow(window);
            content.Children.Add(selectButton);

            card.Child = content;
            WindowsPanel.Children.Add(card);
        }
    }

    private void SelectWindow(WindowModel window)
    {
        _selectedWindow = window;
        SelectionText.Text = $"Selected: Window {window.WindowNumber}";
        StartShiftButton.IsEnabled = true;
        RenderWindows();
    }

    private async void StartShiftButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWindow is null)
        {
            return;
        }

        SetLoading(true);
        HideError();
        try
        {
            var response = await _staffService.StartShiftAsync(_selectedWindow.WindowId);
            if (response.Shift is null)
            {
                throw new Exception("Shift information was not returned.");
            }

            SessionManager.CurrentShift = response.Shift;
            new StaffDashboardWindow().Show();
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            await LoadWindowsAsync();
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool isLoading)
    {
        StartShiftButton.IsEnabled = !isLoading && _selectedWindow is not null;
        WindowsPanel.IsEnabled = !isLoading;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void HideError() => ErrorText.Visibility = Visibility.Collapsed;
}
