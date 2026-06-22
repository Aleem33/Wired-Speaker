namespace CableSpeaker.Core;

public interface IAudioFrameSource : IAsyncDisposable
{
    event EventHandler<AudioFrameEventArgs>? FrameReady;

    string Description { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync();
}

