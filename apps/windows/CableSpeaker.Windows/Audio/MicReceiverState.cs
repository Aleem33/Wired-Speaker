namespace CableSpeaker.Windows.Audio;

public sealed record MicReceiverState(
    bool IsRunning,
    bool HasClient,
    string Message,
    float Peak,
    long FramesReceived,
    long FramesDropped,
    int BufferedMilliseconds);
