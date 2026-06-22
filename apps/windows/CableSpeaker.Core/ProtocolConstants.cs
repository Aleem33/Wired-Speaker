namespace CableSpeaker.Core;

public static class ProtocolConstants
{
    public const int Port = SpeakerPort;
    public const int SpeakerPort = 38271;
    public const int MicPort = 38272;
    public const string MagicText = "CSB1";
    public const string MicMagicText = "CSM1";
    public static readonly byte[] Magic = "CSB1"u8.ToArray();
    public static readonly byte[] MicMagic = "CSM1"u8.ToArray();

    public const int SampleRate = 48_000;
    public const int Channels = 2;
    public const int MicChannels = 1;
    public const int BitsPerSample = 16;
    public const int BytesPerSample = BitsPerSample / 8;
    public const int FrameDurationMs = 20;
    public const int SamplesPerChannelPerFrame = SampleRate * FrameDurationMs / 1000;
    public const int FramePayloadBytes = SamplesPerChannelPerFrame * Channels * BytesPerSample;
    public const int MicFramePayloadBytes = SamplesPerChannelPerFrame * MicChannels * BytesPerSample;

    public const int HandshakeBytes = 20;
    public const int FrameHeaderBytes = 12;
    public const int MaxFramePayloadBytes = FramePayloadBytes * 8;
    public const int MaxMicFramePayloadBytes = MicFramePayloadBytes * 8;
}
