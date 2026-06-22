using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace CableSpeaker.Core;

public sealed class CableSpeakerServer : IAsyncDisposable
{
    private readonly IAudioFrameSource _source;
    private readonly IPAddress _address;
    private readonly int _requestedPort;
    private readonly Channel<AudioFrame> _frames;
    private readonly object _stateLock = new();

    private CancellationTokenSource? _cts;
    private TcpListener? _listener;
    private Task? _acceptLoop;
    private long _framesSent;
    private long _framesDropped;
    private float _peak;
    private bool _hasClient;

    public CableSpeakerServer(IAudioFrameSource source, int port = ProtocolConstants.Port)
        : this(source, IPAddress.Loopback, port)
    {
    }

    public CableSpeakerServer(IAudioFrameSource source, IPAddress address, int port)
    {
        _source = source;
        _address = address;
        _requestedPort = port;
        _frames = Channel.CreateBounded<AudioFrame>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        _source.FrameReady += OnFrameReady;
    }

    public event EventHandler<ServerStateEventArgs>? StateChanged;

    public bool IsRunning => _cts is not null;

    public int Port
    {
        get
        {
            if (_listener?.LocalEndpoint is IPEndPoint endpoint)
            {
                return endpoint.Port;
            }

            return _requestedPort;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is not null)
        {
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new TcpListener(_address, _requestedPort);
        _listener.Start();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token), CancellationToken.None);
        await _source.StartAsync(_cts.Token).ConfigureAwait(false);
        PublishState("Waiting for Android phone connection.");
    }

    public async Task StopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        await _cts.CancelAsync().ConfigureAwait(false);
        _listener?.Stop();

        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }
        }

        await _source.StopAsync().ConfigureAwait(false);
        _cts.Dispose();
        _cts = null;
        _acceptLoop = null;
        _listener = null;

        lock (_stateLock)
        {
            _hasClient = false;
            _peak = 0;
        }

        PublishState("Stopped.");
    }

    public async ValueTask DisposeAsync()
    {
        _source.FrameReady -= OnFrameReady;
        await StopAsync().ConfigureAwait(false);
        await _source.DisposeAsync().ConfigureAwait(false);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var client = await _listener!.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            WireProtocol.EnableTcpNoDelay(client);

            lock (_stateLock)
            {
                _hasClient = true;
            }

            PublishState("Android phone connected.");

            try
            {
                await using var stream = client.GetStream();
                await WireProtocol.WriteHandshakeAsync(stream, cancellationToken).ConfigureAwait(false);

                await foreach (var frame in _frames.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    await WireProtocol.WriteFrameAsync(stream, frame, cancellationToken).ConfigureAwait(false);
                    Interlocked.Increment(ref _framesSent);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or EndOfStreamException)
            {
                PublishState("Android phone disconnected. Waiting for reconnect.");
            }
            finally
            {
                lock (_stateLock)
                {
                    _hasClient = false;
                }
            }
        }
    }

    private void OnFrameReady(object? sender, AudioFrameEventArgs e)
    {
        lock (_stateLock)
        {
            _peak = e.Frame.Peak;
        }

        if (!_frames.Writer.TryWrite(e.Frame))
        {
            Interlocked.Increment(ref _framesDropped);
        }

        PublishState(_hasClient ? "Streaming audio to Android phone." : "Waiting for Android phone connection.");
    }

    private void PublishState(string message)
    {
        ServerState state;
        lock (_stateLock)
        {
            state = new ServerState(
                IsRunning,
                _hasClient,
                message,
                _peak,
                Interlocked.Read(ref _framesSent),
                Interlocked.Read(ref _framesDropped));
        }

        StateChanged?.Invoke(this, new ServerStateEventArgs(state));
    }
}

