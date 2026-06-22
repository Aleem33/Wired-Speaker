using System.Net;
using System.Net.Sockets;
using CableSpeaker.Core;

namespace CableSpeaker.Core.Tests;

public sealed class CableSpeakerServerTests
{
    [Fact]
    public async Task AllowsClientReconnects()
    {
        await using var source = new SineWaveFrameSource();
        await using var server = new CableSpeakerServer(source, IPAddress.Loopback, port: 0);
        await server.StartAsync();

        await ConnectReadFrameAndCloseAsync(server.Port);
        await ConnectReadFrameAndCloseAsync(server.Port);

        await server.StopAsync();
    }

    private static async Task ConnectReadFrameAndCloseAsync(int port)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
        await using var stream = client.GetStream();

        var handshake = await WireProtocol.ReadHandshakeAsync(stream, timeout.Token);
        handshake.Validate();

        var frame = await WireProtocol.ReadFrameAsync(stream, timeout.Token);
        Assert.Equal(ProtocolConstants.FramePayloadBytes, frame.Payload.Length);
    }
}

