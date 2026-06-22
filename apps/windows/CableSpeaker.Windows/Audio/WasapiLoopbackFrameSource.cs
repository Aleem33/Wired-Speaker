using CableSpeaker.Core;
using NAudio.Wave;

namespace CableSpeaker.Windows.Audio;

public sealed class WasapiLoopbackFrameSource : IAudioFrameSource
{
    private WasapiLoopbackCapture? _capture;
    private Pcm16FrameEncoder? _encoder;

    public event EventHandler<AudioFrameEventArgs>? FrameReady;

    public string Description => _capture?.WaveFormat is null
        ? "Windows default output"
        : $"Windows default output ({_capture.WaveFormat.SampleRate} Hz, {_capture.WaveFormat.Channels} ch)";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_capture is not null)
        {
            return Task.CompletedTask;
        }

        _capture = new WasapiLoopbackCapture();
        _encoder = new Pcm16FrameEncoder(_capture.WaveFormat);
        _capture.DataAvailable += Capture_DataAvailable;
        _capture.RecordingStopped += Capture_RecordingStopped;
        _capture.StartRecording();
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_capture is null)
        {
            return Task.CompletedTask;
        }

        _capture.StopRecording();
        CleanupCapture();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        CleanupCapture();
        return ValueTask.CompletedTask;
    }

    private void Capture_DataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_encoder is null || e.BytesRecorded <= 0)
        {
            return;
        }

        var frames = _encoder.Encode(e.Buffer.AsSpan(0, e.BytesRecorded));
        foreach (var frame in frames)
        {
            FrameReady?.Invoke(this, new AudioFrameEventArgs(frame));
        }
    }

    private void Capture_RecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            throw new InvalidOperationException("Windows audio capture stopped unexpectedly.", e.Exception);
        }
    }

    private void CleanupCapture()
    {
        if (_capture is null)
        {
            return;
        }

        _capture.DataAvailable -= Capture_DataAvailable;
        _capture.RecordingStopped -= Capture_RecordingStopped;
        _capture.Dispose();
        _capture = null;
        _encoder = null;
    }
}

