namespace CableSpeaker.Windows.Audio;

public sealed class MicReceiverStateEventArgs : EventArgs
{
    public MicReceiverStateEventArgs(MicReceiverState state)
    {
        State = state;
    }

    public MicReceiverState State { get; }
}
