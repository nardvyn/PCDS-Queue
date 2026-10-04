using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCDSQueue.Admin.Models;
using PCDSQueue.Admin.Services;

namespace PCDSQueue.Admin.Views;

public partial class SettingsWindow : UserControl
{
    private readonly SettingsService _settingsService = new();

    public SettingsWindow()
    {
        InitializeComponent();
    }

    public async Task RefreshAsync()
    {
        SettingsSavedText.Visibility = Visibility.Collapsed;
        await RefreshSystemHealthAsync();
        try
        {
            var response = await _settingsService.GetSettingsAsync();
            ApplySettings(response.Settings);
            ApplicationNameText.Text = response.SystemInfo.Application;
            EnvironmentText.Text = response.SystemInfo.Environment;
            UpdateSystemStatus(response.SystemInfo.ApiServer, response.SystemInfo.Database);
            SettingsErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async Task RefreshSystemHealthAsync()
    {
        try
        {
            var health = await _settingsService.GetHealthAsync();
            UpdateSystemStatus(health.ApiServer, health.Database);
        }
        catch
        {
            UpdateSystemStatus("Offline", "Unavailable");
        }
    }

    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(QueueDigitsTextBox.Text, out var queueDigits) || queueDigits is < 2 or > 6)
        {
            ShowError("Queue number digits must be from 2 to 6.");
            return;
        }
        if (!int.TryParse(QrExpirationHoursTextBox.Text, out var qrHours) || qrHours is < 1 or > 168)
        {
            ShowError("QR expiration must be from 1 to 168 hours.");
            return;
        }
        if (!int.TryParse(RefreshIntervalTextBox.Text, out var refreshInterval) || refreshInterval is < 1 or > 60)
        {
            ShowError("Refresh interval must be from 1 to 60 seconds.");
            return;
        }

        SaveSettingsButton.IsEnabled = false;
        SettingsSavedText.Visibility = Visibility.Collapsed;
        try
        {
            var settings = new SystemSettingsModel
            {
                QueueDigits = queueDigits,
                QrExpirationHours = qrHours,
                TvVoiceEnabled = TvVoiceCheckBox.IsChecked == true,
                RefreshInterval = refreshInterval,
                MobileQueueEnabled = MobileQueueCheckBox.IsChecked == true,
                KioskQueueEnabled = KioskQueueCheckBox.IsChecked == true
            };

            var response = await _settingsService.SaveSettingsAsync(settings);
            ApplySettings(response.Settings);
            ApplicationNameText.Text = response.SystemInfo.Application;
            EnvironmentText.Text = response.SystemInfo.Environment;
            UpdateSystemStatus(response.SystemInfo.ApiServer, response.SystemInfo.Database);
            SettingsErrorText.Visibility = Visibility.Collapsed;
            SettingsSavedText.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            SaveSettingsButton.IsEnabled = true;
        }
    }

    private void ApplySettings(SystemSettingsModel settings)
    {
        QueueDigitsTextBox.Text = settings.QueueDigits.ToString();
        QrExpirationHoursTextBox.Text = settings.QrExpirationHours.ToString();
        TvVoiceCheckBox.IsChecked = settings.TvVoiceEnabled;
        RefreshIntervalTextBox.Text = settings.RefreshInterval.ToString();
        MobileQueueCheckBox.IsChecked = settings.MobileQueueEnabled;
        KioskQueueCheckBox.IsChecked = settings.KioskQueueEnabled;
    }

    private void UpdateSystemStatus(string apiServer, string database)
    {
        ApiStatusText.Text = apiServer;
        DatabaseStatusText.Text = database;
        ApiStatusDot.Fill = IsOnline(apiServer) ? Brushes.SeaGreen : Brushes.IndianRed;
        DatabaseStatusDot.Fill = IsOnline(database) ? Brushes.SeaGreen : Brushes.IndianRed;
    }

    private static bool IsOnline(string status) =>
        string.Equals(status, "Online", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Connected", StringComparison.OrdinalIgnoreCase);

    private void ShowError(string message)
    {
        SettingsErrorText.Text = message;
        SettingsErrorText.Visibility = Visibility.Visible;
        SettingsSavedText.Visibility = Visibility.Collapsed;
    }
}