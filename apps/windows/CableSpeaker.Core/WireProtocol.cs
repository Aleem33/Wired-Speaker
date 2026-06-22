using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace CableSpeaker.Core;

public static class WireProtocol
{
    public static byte[] CreateHandshake()
    {
        var buffer = new byte[ProtocolConstants.HandshakeBytes];
        ProtocolConstants.Magic.CopyTo(buffer, 0);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4, 4), ProtocolConstants.SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(8, 4), ProtocolConstants.Channels);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(12, 4), ProtocolConstants.BitsPerSample);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(16, 4), ProtocolConstants.FrameDurationMs);
        return buffer;
    }

    public static Handshake ParseHandshake(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < ProtocolConstants.HandshakeBytes)
        {
            throw new InvalidDataException("Handshake is incomplete.");
        }

        if (!buffer[..4].SequenceEqual(ProtocolConstants.Magic))
        {
            var magic = Encoding.ASCII.GetString(buffer[..4]);
            throw new InvalidDataException($"Unexpected protocol magic '{magic}'.");
        }

        return new Handshake(
            BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(4, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(8, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(12, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(16, 4)));
    }

    public static async ValueTask WriteHandshakeAsync(Stream stream, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(CreateHandshake(), cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<Handshake> ReadHandshakeAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[ProtocolConstants.HandshakeBytes];
        await ReadExactlyAsync(stream, buffer, cancellationToken).ConfigureAwait(false);
        return ParseHandshake(buffer);
    }

    public static async ValueTask WriteFrameAsync(Stream stream, AudioFrame frame, CancellationToken cancellationToken)
    {
        if (frame.Payload.Length is <= 0 or > ProtocolConstants.MaxFramePayloadBytes)
        {
            throw new InvalidDataException($"Invalid frame payload length {frame.Payload.Length}.");
        }

        var header = new byte[ProtocolConstants.FrameHeaderBytes];
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0, 4), frame.Payload.Length);
        BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(4, 8), frame.HostTimestampMicros);

        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(frame.Payload, cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<AudioFrame> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[ProtocolConstants.FrameHeaderBytes];
        await ReadExactlyAsync(stream, header, cancellationToken).ConfigureAwait(false);

        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0, 4));
        if (payloadLength is <= 0 or > ProtocolConstants.MaxFramePayloadBytes)
        {
            throw new InvalidDataException($"Invalid frame payload length {payloadLength}.");
        }

        var timestamp = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(4, 8));
        var payload = new byte[payloadLength];
        await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        return new AudioFrame(payload, timestamp, Peak: 0);
    }

    public static async ValueTask ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("The peer closed the connection.");
            }

            offset += read;
        }
    }

    public static void EnableTcpNoDelay(TcpClient client)
    {
        client.NoDelay = true;
        client.ReceiveBufferSize = ProtocolConstants.FramePayloadBytes * 16;
        client.SendBufferSize = ProtocolConstants.FramePayloadBytes * 16;
    }
}

