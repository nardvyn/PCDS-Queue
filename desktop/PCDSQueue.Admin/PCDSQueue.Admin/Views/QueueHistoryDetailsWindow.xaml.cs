using System.Windows;
using PCDSQueue.Admin.Models;

namespace PCDSQueue.Admin.Views;

public partial class QueueHistoryDetailsWindow : Window
{
    public QueueHistoryDetailsWindow(AdminQueueHistoryDetailResponse response)
    {
        InitializeComponent();

        var transaction = response.Transaction;
        Title = $"Queue {transaction.QueueNumber} Details";
        QueueNumberText.Text = $"QUEUE {transaction.QueueNumber}";
        StatusText.Text = transaction.Status;
        SourceText.Text = transaction.Source;
        DepartmentText.Text = transaction.DepartmentName;
        WindowText.Text = transaction.WindowDisplay;
        StaffText.Text = transaction.StaffDisplay;
        GeneratedText.Text = transaction.CreatedDisplay;
        CalledText.Text = transaction.CalledDisplay;
        CompletedText.Text = transaction.CompletedDisplay;
        WaitingDurationText.Text = transaction.WaitingDurationDisplay;
        ServiceDurationText.Text = transaction.ServiceDurationDisplay;
        EventsGrid.ItemsSource = response.Events;
    }
}