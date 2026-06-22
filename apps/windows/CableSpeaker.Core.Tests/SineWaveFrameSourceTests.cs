using CableSpeaker.Core;

namespace CableSpeaker.Core.Tests;

public sealed class SineWaveFrameSourceTests
{
    [Fact]
    public async Task EmitsTwentyMillisecondPcmFrames()
    {
        await using var source = new SineWaveFrameSource();
        var tcs = new TaskCompletionSource<AudioFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.FrameReady += (_, e) => tcs.TrySetResult(e.Frame);

        await source.StartAsync(CancellationToken.None);
        var frame = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await source.StopAsync();

        Assert.Equal(ProtocolConstants.FramePayloadBytes, frame.Payload.Length);
        Assert.InRange(frame.Peak, 0.01f, 1f);
    }
}

