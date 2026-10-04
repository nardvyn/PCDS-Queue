using System;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using PCDSQueue.TVDisplay.Models;

namespace PCDSQueue.TVDisplay.Services;

public sealed class VoiceService : IDisposable
{
    private readonly SpeechSynthesizer _speech = new();

    public VoiceService()
    {
        _speech.Volume = 100;
        _speech.Rate = -1;
    }

    public Task AnnounceAsync(AnnouncementEventModel queue)
    {
        if (queue is null)
            return Task.CompletedTask;

        var spokenNumber = FormatQueueNumber(queue.QueueNumber);
        var window = queue.WindowNumber is int windowNumber
            ? $"Window {windowNumber}"
            : queue.WindowName ?? "the assigned window";
        var message =
            $"Queue number {spokenNumber}. Please proceed to {queue.DepartmentName}, {window}.";

        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<SpeakCompletedEventArgs>? handler = null;
        handler = (_, _) =>
        {
            _speech.SpeakCompleted -= handler;
            completion.TrySetResult(true);
        };

        _speech.SpeakCompleted += handler;
        try
        {
            _speech.SpeakAsync(message);
        }
        catch
        {
            _speech.SpeakCompleted -= handler;
            completion.TrySetResult(false);
        }

        return completion.Task;
    }

    public void Cancel() => _speech.SpeakAsyncCancelAll();

    private static string FormatQueueNumber(string queueNumber)
    {
        if (string.IsNullOrWhiteSpace(queueNumber))
            return string.Empty;

        return string.Join(
            " ",
            queueNumber.ToUpperInvariant().Select(character => char.IsDigit(character)
                ? DigitToWord(character)
                : character.ToString()));
    }

    private static string DigitToWord(char digit) => digit switch
    {
        '0' => "zero",
        '1' => "one",
        '2' => "two",
        '3' => "three",
        '4' => "four",
        '5' => "five",
        '6' => "six",
        '7' => "seven",
        '8' => "eight",
        '9' => "nine",
        _ => digit.ToString()
    };

    public void Dispose() => _speech.Dispose();
}