using System;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCDSQueue.Kiosk.Models;

namespace PCDSQueue.Kiosk.Services;

public sealed class ThermalPrinterService
{
    private const double TicketWidth = 219;
    private const string PrinterNameVariable = "PCDS_THERMAL_PRINTER";

    public bool Print(QueueTicketModel ticket)
    {
        try
        {
            using var server = new LocalPrintServer();
            using var printer = GetPrinter(server);
            if (printer is null || IsVirtualPrinter(printer.Name))
                return false;

            var ticketVisual = Create58mmTicket(ticket);
            ticketVisual.Measure(new System.Windows.Size(TicketWidth, double.PositiveInfinity));
            ticketVisual.Arrange(new Rect(0, 0, TicketWidth, ticketVisual.DesiredSize.Height));
            ticketVisual.UpdateLayout();

            var requestedTicket = printer.DefaultPrintTicket;
            requestedTicket.PageMediaSize = new PageMediaSize(TicketWidth, ticketVisual.DesiredSize.Height);
            var validatedTicket = printer.MergeAndValidatePrintTicket(
                printer.DefaultPrintTicket,
                requestedTicket).ValidatedPrintTicket;

            if (validatedTicket.PageMediaSize?.Width is not double mediaWidth
                || Math.Abs(mediaWidth - TicketWidth) > 1)
            {
                return false;
            }

            var writer = PrintQueue.CreateXpsDocumentWriter(printer);
            writer.Write(ticketVisual, validatedTicket);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static PrintQueue? GetPrinter(LocalPrintServer server)
    {
        var configuredName = Environment.GetEnvironmentVariable(PrinterNameVariable)?.Trim();
        var queues = server.GetPrintQueues();
        if (!string.IsNullOrWhiteSpace(configuredName))
        {
            return queues.FirstOrDefault(queue =>
                queue.Name.Equals(configuredName, StringComparison.OrdinalIgnoreCase));
        }

        try
        {
            return LocalPrintServer.GetDefaultPrintQueue();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsVirtualPrinter(string printerName)
    {
        var name = printerName.ToUpperInvariant();
        return name.Contains("PDF")
            || name.Contains("XPS")
            || name.Contains("ONENOTE")
            || name.Contains("FAX");
    }

    private static Border Create58mmTicket(QueueTicketModel ticket)
    {
        var container = new Border
        {
            Width = TicketWidth,
            Background = System.Windows.Media.Brushes.White,
            Padding = new Thickness(8)
        };
        var panel = new StackPanel();
        var printedAt = DateTime.Now;
        var departmentName = ticket.Department?.DepartmentName ?? "PCDS";

        panel.Children.Add(Text("PCDS", 18, FontWeights.Bold));
        panel.Children.Add(Text("QUEUE NUMBERING SYSTEM", 11, FontWeights.Bold));
        panel.Children.Add(Text("------------------------------", 10));
        panel.Children.Add(Text(departmentName.ToUpperInvariant(), 15, FontWeights.Bold, 12));
        panel.Children.Add(Text("QUEUE NUMBER", 10, FontWeights.SemiBold, 6));
        panel.Children.Add(Text(ticket.QueueNumber, 38, FontWeights.Bold, 3));
        panel.Children.Add(Text($"People Ahead: {ticket.PeopleAhead}", 11, FontWeights.SemiBold, 10));
        panel.Children.Add(Text($"Date: {printedAt:MMMM dd, yyyy}", 9, topMargin: 14));
        panel.Children.Add(Text($"Time: {printedAt:h:mm tt}", 9));
        panel.Children.Add(Text("------------------------------", 10, topMargin: 10));
        panel.Children.Add(Text("Please wait for your number\nto be displayed on the screen.", 9, topMargin: 5));
        panel.Children.Add(Text("Thank you!", 10, FontWeights.Bold, 10));
        panel.Children.Add(new Border { Height = 25 });
        container.Child = panel;
        return container;
    }

    private static TextBlock Text(
        string value,
        double size,
        FontWeight? weight = null,
        double topMargin = 2)
    {
        return new TextBlock
        {
            Text = value,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = size,
            FontWeight = weight ?? FontWeights.Normal,
            Foreground = System.Windows.Media.Brushes.Black,
            Background = System.Windows.Media.Brushes.White,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, topMargin, 0, 0)
        };
    }
}