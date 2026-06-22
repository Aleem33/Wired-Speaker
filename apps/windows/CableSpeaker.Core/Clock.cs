namespace CableSpeaker.Core;

public static class Clock
{
    public static long UnixTimestampMicros()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
    }
}

