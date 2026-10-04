using System;
using System.IO;
using System.Media;

namespace PCDSQueue.TVDisplay.Services;

public sealed class SoundService : IDisposable
{
    private readonly SoundPlayer _player;

    public SoundService()
    {
        var path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Assets",
            "notification.wav");
        _player = new SoundPlayer(path);
    }

    public void PlayDing()
    {
        try
        {
            _player.Play();
        }
        catch
        {
            // Keep the display and voice flow available if the chime cannot play.
        }
    }

    public void Stop()
    {
        try
        {
            _player.Stop();
        }
        catch
        {
        }
    }

    public void Dispose() => _player.Dispose();
}