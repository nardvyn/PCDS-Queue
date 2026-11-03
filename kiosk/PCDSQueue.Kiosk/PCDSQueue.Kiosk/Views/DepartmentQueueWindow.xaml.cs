using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PCDSQueue.Kiosk.Models;
using PCDSQueue.Kiosk.Services;
using QRCoder;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;

namespace PCDSQueue.Kiosk.Views;

public partial class DepartmentQueueWindow : Window
{
    private readonly DepartmentModel _department;
    private readonly DepartmentQueueService _departmentQueueService = new();
    private readonly QueueService _queueService = new();
    private readonly ThermalPrinterService _thermalPrinterService = new();
    private readonly DispatcherTimer _refreshTimer = new()
    {
        Interval = TimeSpan.FromSeconds(3)
    };
    private bool _refreshing;
    private bool _checkingQrSession;
    private bool _isGeneratingTicket;
    private bool _queueOpen;
    private bool _kioskQueueEnabled = true;
    private bool _queueAtCapacity;
    private bool _dailyQueueLimitReached;
    private string? _qrSessionToken;
    private QueueTicketModel? _pendingTicket;

    public DepartmentQueueWindow(DepartmentModel department)
    {
        InitializeComponent();
        _department = department;
        DepartmentNameText.Text = department.DepartmentName.ToUpperInvariant();
        _refreshTimer.Tick += RefreshTimer_Tick;
        Loaded += DepartmentQueueWindow_Loaded;
    }

    private async void DepartmentQueueWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshDepartmentScreenAsync();
        _refreshTimer.Start();
    }

    private async Task RefreshDepartmentScreenAsync()
    {
        await CheckServerAsync();
        if (!IsServerOnline())
            return;

        await RefreshQueueStatusAsync();
        if (_canAcceptQueueEntry() && QrImage.Source is null)
            await LoadQrAsync();
    }

    private async Task CheckServerAsync()
    {
        var online = await ApiService.IsServerOnlineAsync();
        var color = online ? Color.FromRgb(22, 163, 106) : Color.FromRgb(220, 53, 69);
        ServerStatusDot.Fill = new SolidColorBrush(color);
        ServerStatusText.Text = online ? "Server Online" : "Server Offline";
        ServerStatusText.Foreground = new SolidColorBrush(color);
        if (!online)
        {
            PrintTicketButton.IsEnabled = _pendingTicket is not null && !_isGeneratingTicket;
            QueueStateText.Text = "Server unavailable";
        }
    }

    private bool IsServerOnline() => ServerStatusText.Text == "Server Online";

    private async Task LoadQrAsync()
    {
        QrLoadingText.Visibility = Visibility.Visible;
        try
        {
            var session = await _departmentQueueService.CreateQrSessionAsync(_department.DepartmentId);
            _qrSessionToken = session.Token;
            QrImage.Source = CreateQrImage(session.Token);
            QrLoadingText.Visibility = Visibility.Collapsed;
            ErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            QrLoadingText.Text = "QR unavailable";
            ShowError(ex.Message);
        }
    }

    private async Task RefreshQueueStatusAsync()
    {
        if (_refreshing)
            return;

        _refreshing = true;
        try
        {
            var status = await _departmentQueueService.GetStatusAsync(_department.DepartmentId);
            WaitingCountText.Text = status.WaitingCount.ToString();
            _queueOpen = status.Department?.QueueIsOpen == true;
            _kioskQueueEnabled = status.Department?.KioskQueueEnabled ?? true;
            _queueAtCapacity = status.ActiveQueueCount >= status.ActiveQueueLimit;
            _dailyQueueLimitReached = status.DailyQueueCount >= status.DailyQueueLimit;
            QueueCapacityText.Text =
                $"ACTIVE: {status.ActiveQueueCount} / {status.ActiveQueueLimit}   TODAY: {status.DailyQueueCount} / {status.DailyQueueLimit}";
            QueueStateText.Text = _queueAtCapacity
                ? "Queue is full. Please wait for an available ticket."
                : _dailyQueueLimitReached
                ? "Today's queue number limit has been reached."
                : !_kioskQueueEnabled
                ? "Kiosk queue unavailable"
                : _queueOpen ? "Queue is open" : "Queue is currently closed";
            PrintTicketButtonText.Text = _pendingTicket is not null
                ? "RETRY PRINT"
                : _queueAtCapacity ? "QUEUE FULL — PLEASE WAIT"
                : _dailyQueueLimitReached ? "DAILY LIMIT REACHED"
                : _kioskQueueEnabled ? "PRINT TICKET" : "KIOSK QUEUE UNAVAILABLE";
            PrintTicketButton.IsEnabled = _pendingTicket is not null
                || (_canAcceptQueueEntry() && !_isGeneratingTicket && IsServerOnline());
            if (!_canAcceptQueueEntry())
            {
                QrImage.Source = null;
                _qrSessionToken = null;
                QrLoadingText.Text = _queueAtCapacity
                    ? "This department's queue is full. Please wait."
                    : _dailyQueueLimitReached
                    ? "Today's queue number limit has been reached."
                    : "Queue entry is currently unavailable.";
                QrLoadingText.Visibility = Visibility.Visible;
            }
            else if (QrImage.Source is null)
            {
                QrLoadingText.Text = "Preparing secure QR...";
            }
            ErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            PrintTicketButton.IsEnabled = _pendingTicket is not null && !_isGeneratingTicket;
            QueueStateText.Text = "Unable to refresh queue status";
            ShowError(ex.Message);
            await CheckServerAsync();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        await CheckServerAsync();
        if (IsServerOnline())
        {
            await RefreshQueueStatusAsync();
            if (_canAcceptQueueEntry() && QrImage.Source is null)
                await LoadQrAsync();

            if (_pendingTicket is null && await IsQrSessionScannedAsync())
            {
                ReturnToHome();
                return;
            }
        }
    }

    private bool _canAcceptQueueEntry() =>
        _queueOpen && _kioskQueueEnabled && !_queueAtCapacity && !_dailyQueueLimitReached;

    private async Task<bool> IsQrSessionScannedAsync()
    {
        if (_checkingQrSession || string.IsNullOrWhiteSpace(_qrSessionToken))
            return false;

        _checkingQrSession = true;
        try
        {
            var status = await _departmentQueueService.GetQrSessionStatusAsync(_qrSessionToken);
            return status.Scanned;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            return false;
        }
        finally
        {
            _checkingQrSession = false;
        }
    }

    private async void PrintTicketButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isGeneratingTicket || (_pendingTicket is null
            && (!IsServerOnline() || !_canAcceptQueueEntry())))
            return;

        _isGeneratingTicket = true;
        PrintTicketButton.IsEnabled = false;
        PrintTicketButtonText.Text = _pendingTicket is null ? "GENERATING..." : "PRINTING...";
        try
        {
            if (_pendingTicket is null)
                _pendingTicket = await _queueService.GenerateQueueAsync(_department.DepartmentId);
            GeneratedTicketNumberText.Text = _pendingTicket.QueueNumber;
            GeneratedTicketPanel.Visibility = Visibility.Visible;

            PrintTicketButtonText.Text = "PRINTING...";
            await Dispatcher.Yield(DispatcherPriority.Render);
            if (!_thermalPrinterService.Print(_pendingTicket))
            {
                var message =
                    $"Queue number {_pendingTicket.QueueNumber} was created, but the ticket could not be printed. " +
                    "Check the thermal printer, then press RETRY PRINT.";
                QueueStateText.Text = "Ticket generated, but not printed";
                ShowError(message);
                MessageBox.Show(
                    message,
                    "Printer Unavailable",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            _refreshTimer.Stop();
            new TicketWindow(_pendingTicket).Show();
            Close();
        }
        catch (Exception ex)
        {
            var message = _pendingTicket is null
                ? ex.Message
                : $"Queue {_pendingTicket.QueueNumber} was created, but printing failed. The number is shown above. Retry to print the same ticket. {ex.Message}";
            QueueStateText.Text = _pendingTicket is null ? "Unable to create ticket" : "Ticket generated, but not printed";
            ShowError(message);
            if (_pendingTicket is null)
            {
                await CheckServerAsync();
                await RefreshQueueStatusAsync();
            }
        }
        finally
        {
            _isGeneratingTicket = false;
            if (IsVisible)
            {
                PrintTicketButtonText.Text = _pendingTicket is not null
                    ? "RETRY PRINT"
                    : _queueAtCapacity ? "QUEUE FULL — PLEASE WAIT"
                    : _dailyQueueLimitReached ? "DAILY LIMIT REACHED"
                    : _kioskQueueEnabled ? "PRINT TICKET" : "KIOSK QUEUE UNAVAILABLE";
                PrintTicketButton.IsEnabled = _pendingTicket is not null
                    || (_canAcceptQueueEntry() && IsServerOnline());
            }
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingTicket is not null && MessageBox.Show(
                $"Queue {_pendingTicket.QueueNumber} has already been generated but not confirmed printed. Return home anyway?",
                "Ticket not printed",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        ReturnToHome();
    }

    private void ReturnToHome()
    {
        _refreshTimer.Stop();
        new MainWindow().Show();
        Close();
    }

    private static BitmapImage CreateQrImage(string token)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(token, QRCodeGenerator.ECCLevel.Q);
        var pngBytes = new PngByteQRCode(data).GetGraphic(10);
        using var stream = new MemoryStream(pngBytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    protected override void OnClosed(EventArgs e)
    {
        _refreshTimer.Stop();
        base.OnClosed(e);
    }
}