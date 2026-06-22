using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using CableSpeaker.Core;
using NAudio.Wave;

namespace CableSpeaker.Windows.Audio;

public sealed class MicReceiverService : IAsyncDisposable
{
    private readonly int _port;
    private readonly int _waveOutDeviceNumber;
    private readonly object _stateLock = new();

    private CancellationTokenSource? _cts;
    private TcpListener? _listener;
    private Task? _acceptLoop;
    private BufferedWaveProvider? _buffer;
    private WaveOutEvent? _waveOut;
    private bool _hasClient;
    private float _peak;
    private long _framesReceived;
    private long _framesDropped;

    public MicReceiverService(int waveOutDeviceNumber, int port = ProtocolConstants.MicPort)
    {
        _waveOutDeviceNumber = waveOutDeviceNumber;
        _port = port;
    }

    public event EventHandler<MicReceiverStateEventArgs>? StateChanged;

    public bool IsRunning => _cts is not null;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is not null)
        {
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _buffer = new BufferedWaveProvider(new WaveFormat(ProtocolConstants.SampleRate, ProtocolConstants.BitsPerSample, ProtocolConstants.MicChannels))
        {
            BufferDuration = TimeSpan.FromMilliseconds(500),
            DiscardOnBufferOverflow = true
        };
        _waveOut = new WaveOutEvent
        {
            DeviceNumber = _waveOutDeviceNumber,
            DesiredLatency = 120
        };
        _waveOut.Init(_buffer);
        _waveOut.Play();

        _listener = new TcpListener(IPAddress.Loopback, _port);
        _listener.Start();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token), CancellationToken.None);
        PublishState($"Mic receiver listening on 127.0.0.1:{_port}.");
        await Task.CompletedTask.ConfigureAwait(false);
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

        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;
        _buffer = null;
        _listener = null;
        _acceptLoop = null;
        _cts.Dispose();
        _cts = null;

        lock (_stateLock)
        {
            _hasClient = false;
            _peak = 0;
        }

        PublishState("Mic receiver stopped.");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
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

            PublishState("Phone mic connected.");

            try
            {
                await using var stream = client.GetStream();
                var handshake = await WireProtocol.ReadMicHandshakeAsync(stream, cancellationToken).ConfigureAwait(false);
                handshake.ValidateMic();

                while (!cancellationToken.IsCancellationRequested)
                {
                    var frame = await WireProtocol.ReadFrameAsync(stream, ProtocolConstants.MaxMicFramePayloadBytes, cancellationToken).ConfigureAwait(false);
                    if (frame.Payload.Length != ProtocolConstants.MicFramePayloadBytes)
                    {
                        Interlocked.Increment(ref _framesDropped);
                        continue;
                    }

                    _buffer?.AddSamples(frame.Payload, 0, frame.Payload.Length);
                    Interlocked.Increment(ref _framesReceived);
                    lock (_stateLock)
                    {
                        _peak = CalculatePeak(frame.Payload);
                    }

                    PublishState("Receiving phone mic.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or EndOfStreamException or InvalidDataException)
            {
                PublishState("Phone mic disconnected. Waiting for reconnect.");
            }
            finally
            {
                lock (_stateLock)
                {
                    _hasClient = false;
                    _peak = 0;
                }
            }
        }
    }

    private void PublishState(string message)
    {
        MicReceiverState state;
        lock (_stateLock)
        {
            state = new MicReceiverState(
                IsRunning,
                _hasClient,
                message,
                _peak,
                Interlocked.Read(ref _framesReceived),
                Interlocked.Read(ref _framesDropped),
                (int)(_buffer?.BufferedDuration.TotalMilliseconds ?? 0));
        }

        StateChanged?.Invoke(this, new MicReceiverStateEventArgs(state));
    }

    private static float CalculatePeak(byte[] payload)
    {
        var peak = 0;
        for (var offset = 0; offset + 1 < payload.Length; offset += 2)
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(offset, 2));
            peak = Math.Max(peak, Math.Abs((int)sample));
        }

        return peak / (float)short.MaxValue;
    }
}
