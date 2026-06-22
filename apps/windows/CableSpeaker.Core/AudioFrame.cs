namespace CableSpeaker.Core;

public sealed record AudioFrame(byte[] Payload, long HostTimestampMicros, float Peak);

