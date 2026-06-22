namespace CableSpeaker.Core;

public sealed class ServerStateEventArgs : EventArgs
{
    public ServerStateEventArgs(ServerState state)
    {
        State = state;
    }

    public ServerState State { get; }
}

