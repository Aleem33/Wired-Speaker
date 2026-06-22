using System.Buffers.Binary;

namespace CableSpeaker.Core;

public sealed class SineWaveFrameSource : IAudioFrameSource
{
    private readonly double _frequency;
    private readonly double _amplitude;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private int _sampleIndex;

    public SineWaveFrameSource(double frequency = 440, double amplitude = 0.25)
    {
        _frequency = frequency;
        _amplitude = Math.Clamp(amplitude, 0, 1);
    }

    public event EventHandler<AudioFrameEventArgs>? FrameReady;

    public string Description => $"{_frequency:0} Hz generated test tone";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_loopTask is not null)
        {
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loopTask = Task.Run(() => RunAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts is null || _loopTask is null)
        {
            return;
        }

        await _cts.CancelAsync().ConfigureAwait(false);
        try
        {
            await _loopTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _loopTask = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(ProtocolConstants.FrameDurationMs));

        EmitFrame();
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            EmitFrame();
        }
    }

    private void EmitFrame()
    {
        var payload = new byte[ProtocolConstants.FramePayloadBytes];
        var peak = 0f;

        for (var sample = 0; sample < ProtocolConstants.SamplesPerChannelPerFrame; sample++)
        {
            var phase = 2 * Math.PI * _frequency * _sampleIndex / ProtocolConstants.SampleRate;
            var value = (short)Math.Round(Math.Sin(phase) * short.MaxValue * _amplitude);
            peak = Math.Max(peak, Math.Abs(value) / (float)short.MaxValue);

            var offset = sample * ProtocolConstants.Channels * ProtocolConstants.BytesPerSample;
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(offset, 2), value);
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(offset + 2, 2), value);
            _sampleIndex++;
        }

        FrameReady?.Invoke(this, new AudioFrameEventArgs(new AudioFrame(payload, Clock.UnixTimestampMicros(), peak)));
    }
}

