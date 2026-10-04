using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PCDSQueue.Admin.Models;
using PCDSQueue.Admin.Services;
using QRCoder;

namespace PCDSQueue.Admin.Views;

public partial class QrManagementWindow : UserControl
{
    private readonly DepartmentService _departmentService = new();
    private readonly QrManagementService _qrService = new();
    private List<DepartmentModel> _departments = [];
    private QrSessionModel? _currentSession;
    private long? _currentSessionId;
    private byte[]? _qrPngBytes;
    private bool _loadingDepartments;

    public QrManagementWindow()
    {
        InitializeComponent();
    }

    public async Task RefreshAsync()
    {
        await LoadDepartmentsAsync();
    }

    private async Task LoadDepartmentsAsync()
    {
        var selectedId = (DepartmentComboBox.SelectedItem as DepartmentModel)?.DepartmentId;
        _loadingDepartments = true;
        try
        {
            _departments = await _departmentService.GetDepartmentsAsync();
            var activeDepartments = _departments.Where(department => department.IsActive).ToList();
            DepartmentComboBox.ItemsSource = activeDepartments;
            DepartmentComboBox.SelectedItem = activeDepartments.FirstOrDefault(
                department => department.DepartmentId == selectedId)
                ?? activeDepartments.FirstOrDefault();
            await LoadCurrentSessionAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            _loadingDepartments = false;
        }
    }

    private async Task LoadCurrentSessionAsync()
    {
        ClearGeneratedQr();
        if (DepartmentComboBox.SelectedItem is not DepartmentModel department)
        {
            _currentSession = null;
            _currentSessionId = null;
            UpdateStatus(null);
            QrDepartmentText.Text = "Select a department";
            GenerateButton.IsEnabled = false;
            DeactivateButton.IsEnabled = false;
            return;
        }

        GenerateButton.IsEnabled = true;
        QrDepartmentText.Text = department.DepartmentName.ToUpperInvariant();
        try
        {
            var sessions = await _qrService.GetSessionsAsync(department.DepartmentId);
            _currentSession = sessions.FirstOrDefault();
            _currentSessionId = _currentSession?.QrSessionId;
            UpdateStatus(_currentSession);
            DeactivateButton.IsEnabled = _currentSession?.Status == "ACTIVE";
            if (_currentSession?.Status == "ACTIVE")
                QrMessageText.Text = "An active QR exists, but its secret is not recoverable. Generate a new QR to display and save it.";
            else
                QrMessageText.Text = "Generate a QR to create a new mobile access token.";
            QrErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async void DepartmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loadingDepartments)
            await LoadCurrentSessionAsync();
    }

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        if (DepartmentComboBox.SelectedItem is not DepartmentModel department)
            return;

        GenerateButton.IsEnabled = false;
        try
        {
            var generated = await _qrService.GenerateAsync(department.DepartmentId);
            _currentSessionId = generated.QrSessionId;
            _qrPngBytes = CreateQrPng(generated.Token);
            DisplayQr(_qrPngBytes);
            CreatedText.Text = generated.CreatedAt?.ToString("MMM d, yyyy h:mm tt") ?? "---";
            ExpiresText.Text = generated.ExpiresAt?.ToString("MMM d, yyyy h:mm tt") ?? "---";
            QrDepartmentText.Text = generated.DepartmentName.ToUpperInvariant();
            QrMessageText.Text = "Scan with the mobile app to validate this department token.";
            StatusText.Text = "ACTIVE";
            StatusDot.Fill = System.Windows.Media.Brushes.SeaGreen;
            SaveQrButton.IsEnabled = true;
            DeactivateButton.IsEnabled = true;
            QrErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            GenerateButton.IsEnabled = DepartmentComboBox.SelectedItem is not null;
        }
    }

    private async void DeactivateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSessionId is null)
            return;

        DeactivateButton.IsEnabled = false;
        try
        {
            await _qrService.DeactivateAsync(_currentSessionId.Value);
            await LoadCurrentSessionAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            DeactivateButton.IsEnabled = _currentSession?.Status == "ACTIVE";
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadDepartmentsAsync();

    private void SaveQrButton_Click(object sender, RoutedEventArgs e)
    {
        if (_qrPngBytes is null || DepartmentComboBox.SelectedItem is not DepartmentModel department)
            return;

        var dialog = new SaveFileDialog
        {
            Title = "Save Department QR Code",
            FileName = $"{MakeFileName(department.DepartmentName)}-queue-qr.png",
            Filter = "PNG image|*.png",
            DefaultExt = ".png",
            AddExtension = true
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            return;

        try
        {
            File.WriteAllBytes(dialog.FileName, _qrPngBytes);
            QrErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private static byte[] CreateQrPng(string token)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(token, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(data);
        return qrCode.GetGraphic(12);
    }

    private void DisplayQr(byte[] pngBytes)
    {
        using var stream = new MemoryStream(pngBytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        QrImage.Source = bitmap;
        QrPlaceholderText.Visibility = Visibility.Collapsed;
    }

    private void ClearGeneratedQr()
    {
        _qrPngBytes = null;
        QrImage.Source = null;
        QrPlaceholderText.Visibility = Visibility.Visible;
        SaveQrButton.IsEnabled = false;
        CreatedText.Text = "---";
        ExpiresText.Text = "---";
    }

    private void UpdateStatus(QrSessionModel? session)
    {
        var status = session?.Status ?? "NO QR SESSION";
        StatusText.Text = status;
        StatusDot.Fill = status switch
        {
            "ACTIVE" => System.Windows.Media.Brushes.SeaGreen,
            "EXPIRED" => System.Windows.Media.Brushes.DarkOrange,
            "REVOKED" => System.Windows.Media.Brushes.IndianRed,
            _ => System.Windows.Media.Brushes.LightGray
        };
        CreatedText.Text = session?.CreatedDisplay ?? "---";
        ExpiresText.Text = session?.ExpiresDisplay ?? "---";
    }

    private static string MakeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        return safe.Replace(' ', '-').ToLowerInvariant();
    }

    private void ShowError(string message)
    {
        QrErrorText.Text = message;
        QrErrorText.Visibility = Visibility.Visible;
    }
}