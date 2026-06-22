using CableSpeaker.Core;

namespace CableSpeaker.Core.Tests;

public sealed class WireProtocolTests
{
    [Fact]
    public void HandshakeRoundTrips()
    {
        var bytes = WireProtocol.CreateHandshake();
        var handshake = WireProtocol.ParseHandshake(bytes);

        Assert.Equal(ProtocolConstants.SampleRate, handshake.SampleRate);
        Assert.Equal(ProtocolConstants.Channels, handshake.Channels);
        Assert.Equal(ProtocolConstants.BitsPerSample, handshake.BitsPerSample);
        Assert.Equal(ProtocolConstants.FrameDurationMs, handshake.FrameDurationMs);
        handshake.Validate();
    }

    [Fact]
    public void MicHandshakeRoundTrips()
    {
        var bytes = WireProtocol.CreateMicHandshake();
        var handshake = WireProtocol.ParseMicHandshake(bytes);

        Assert.Equal(ProtocolConstants.SampleRate, handshake.SampleRate);
        Assert.Equal(ProtocolConstants.MicChannels, handshake.Channels);
        Assert.Equal(ProtocolConstants.BitsPerSample, handshake.BitsPerSample);
        Assert.Equal(ProtocolConstants.FrameDurationMs, handshake.FrameDurationMs);
        handshake.ValidateMic();
    }

    [Fact]
    public async Task FrameRoundTrips()
    {
        var payload = Enumerable.Range(0, ProtocolConstants.FramePayloadBytes)
            .Select(i => (byte)(i % 251))
            .ToArray();
        var original = new AudioFrame(payload, 123_456_789, Peak: 0.5f);

        await using var stream = new MemoryStream();
        await WireProtocol.WriteFrameAsync(stream, original, CancellationToken.None);
        stream.Position = 0;

        var parsed = await WireProtocol.ReadFrameAsync(stream, CancellationToken.None);
        Assert.Equal(original.HostTimestampMicros, parsed.HostTimestampMicros);
        Assert.Equal(original.Payload, parsed.Payload);
    }

    [Fact]
    public void RejectsBadMagic()
    {
        var bytes = WireProtocol.CreateHandshake();
        bytes[0] = (byte)'X';

        Assert.Throws<InvalidDataException>(() => WireProtocol.ParseHandshake(bytes));
    }

    [Fact]
    public async Task RejectsOversizedMicFrame()
    {
        await using var stream = new MemoryStream();
        var header = new byte[ProtocolConstants.FrameHeaderBytes];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0, 4), ProtocolConstants.MaxMicFramePayloadBytes + 1);
        await stream.WriteAsync(header);
        stream.Position = 0;

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await WireProtocol.ReadFrameAsync(stream, ProtocolConstants.MaxMicFramePayloadBytes, CancellationToken.None));
    }
}
