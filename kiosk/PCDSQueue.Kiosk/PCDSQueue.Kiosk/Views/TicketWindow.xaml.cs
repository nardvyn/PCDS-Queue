using System.Windows;
using System.Windows.Threading;
using PCDSQueue.Kiosk.Models;

namespace PCDSQueue.Kiosk.Views;

public partial class TicketWindow : Window
{
    private readonly DispatcherTimer _returnTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };
    private int _secondsRemaining = 5;
    private bool _returningHome;

    public TicketWindow(QueueTicketModel ticket)
    {
        InitializeComponent();
        QueueNumberText.Text = ticket.QueueNumber;
        _returnTimer.Tick += ReturnTimer_Tick;
        _returnTimer.Start();
    }

    private void ReturnTimer_Tick(object? sender, EventArgs e)
    {
        _secondsRemaining--;
        if (_secondsRemaining <= 0)
        {
            ReturnHome();
            return;
        }

        AutoReturnText.Text = $"Returning to home in {_secondsRemaining} seconds...";
    }

    private void DoneButton_Click(object sender, RoutedEventArgs e) => ReturnHome();

    private void ReturnHome()
    {
        if (_returningHome)
            return;

        _returningHome = true;
        _returnTimer.Stop();
        new MainWindow().Show();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _returnTimer.Stop();
        base.OnClosed(e);
    }
}