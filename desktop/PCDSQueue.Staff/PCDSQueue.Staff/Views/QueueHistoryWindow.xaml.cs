using System.Windows;
using PCDSQueue.Staff.Services;

namespace PCDSQueue.Staff.Views;

public partial class QueueHistoryWindow : Window
{
    private readonly QueueService _queueService = new();

    public QueueHistoryWindow()
    {
        InitializeComponent();
        Loaded += QueueHistoryWindow_Loaded;
    }

    private async void QueueHistoryWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _queueService.GetHistoryAsync();
            HistoryDataGrid.ItemsSource = result.History;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Queue History",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => Close();
}
