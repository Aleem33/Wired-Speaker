namespace CableSpeaker.Core;

public sealed class AudioFrameEventArgs : EventArgs
{
    public AudioFrameEventArgs(AudioFrame frame)
    {
        Frame = frame;
    }

    public AudioFrame Frame { get; }
}

