namespace CableSpeaker.Core;

public sealed record ServerState(
    bool IsRunning,
    bool HasClient,
    string Message,
    float Peak,
    long FramesSent,
    long FramesDropped);

