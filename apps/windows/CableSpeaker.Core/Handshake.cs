namespace CableSpeaker.Core;

public sealed record Handshake(
    int SampleRate,
    int Channels,
    int BitsPerSample,
    int FrameDurationMs)
{
    public void Validate()
    {
        if (SampleRate != ProtocolConstants.SampleRate ||
            Channels != ProtocolConstants.Channels ||
            BitsPerSample != ProtocolConstants.BitsPerSample ||
            FrameDurationMs != ProtocolConstants.FrameDurationMs)
        {
            throw new InvalidDataException(
                $"Unsupported stream format {SampleRate} Hz, {Channels} ch, {BitsPerSample}-bit, {FrameDurationMs} ms frames.");
        }
    }

    public void ValidateMic()
    {
        if (SampleRate != ProtocolConstants.SampleRate ||
            Channels != ProtocolConstants.MicChannels ||
            BitsPerSample != ProtocolConstants.BitsPerSample ||
            FrameDurationMs != ProtocolConstants.FrameDurationMs)
        {
            throw new InvalidDataException(
                $"Unsupported mic format {SampleRate} Hz, {Channels} ch, {BitsPerSample}-bit, {FrameDurationMs} ms frames.");
        }
    }
}
