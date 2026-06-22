using System.Buffers.Binary;
using CableSpeaker.Core;
using NAudio.Wave;

namespace CableSpeaker.Windows.Audio;

public sealed class Pcm16FrameEncoder
{
    private readonly List<StereoSample> _sourceSamples = new();
    private readonly byte[] _pendingFrame = new byte[ProtocolConstants.FramePayloadBytes];
    private readonly int _sourceRate;
    private readonly int _sourceChannels;
    private readonly int _bitsPerSample;
    private readonly WaveFormatEncoding _encoding;
    private readonly double _sourceToTargetRatio;

    private double _sourceCursor;
    private int _pendingOffset;
    private float _pendingPeak;

    public Pcm16FrameEncoder(WaveFormat sourceFormat)
    {
        _sourceRate = sourceFormat.SampleRate;
        _sourceChannels = Math.Max(1, sourceFormat.Channels);
        _bitsPerSample = sourceFormat.BitsPerSample;
        _encoding = sourceFormat.Encoding;
        _sourceToTargetRatio = _sourceRate / (double)ProtocolConstants.SampleRate;
    }

    public IReadOnlyList<AudioFrame> Encode(ReadOnlySpan<byte> sourceBytes)
    {
        DecodeToSourceSamples(sourceBytes);

        var frames = new List<AudioFrame>();
        while (_sourceCursor + 1 < _sourceSamples.Count)
        {
            var leftIndex = (int)_sourceCursor;
            var fraction = _sourceCursor - leftIndex;
            var a = _sourceSamples[leftIndex];
            var b = _sourceSamples[leftIndex + 1];
            var left = Lerp(a.Left, b.Left, fraction);
            var right = Lerp(a.Right, b.Right, fraction);

            WriteStereoSample(left, right, out var peak);
            _sourceCursor += _sourceToTargetRatio;

            if (_pendingOffset >= _pendingFrame.Length)
            {
                var payload = _pendingFrame.ToArray();
                frames.Add(new AudioFrame(payload, Clock.UnixTimestampMicros(), _pendingPeak));
                _pendingOffset = 0;
                _pendingPeak = 0;
            }
        }

        DropConsumedSourceSamples();
        return frames;
    }

    private void DecodeToSourceSamples(ReadOnlySpan<byte> sourceBytes)
    {
        var bytesPerSample = Math.Max(1, _bitsPerSample / 8);
        var bytesPerFrame = bytesPerSample * _sourceChannels;
        if (bytesPerFrame <= 0)
        {
            return;
        }

        for (var offset = 0; offset + bytesPerFrame <= sourceBytes.Length; offset += bytesPerFrame)
        {
            var left = ReadSample(sourceBytes.Slice(offset, bytesPerSample));
            var right = _sourceChannels > 1
                ? ReadSample(sourceBytes.Slice(offset + bytesPerSample, bytesPerSample))
                : left;

            _sourceSamples.Add(new StereoSample(left, right));
        }
    }

    private float ReadSample(ReadOnlySpan<byte> sampleBytes)
    {
        if (_encoding == WaveFormatEncoding.IeeeFloat || (_bitsPerSample == 32 && _encoding != WaveFormatEncoding.Pcm))
        {
            return Math.Clamp(BitConverter.ToSingle(sampleBytes), -1f, 1f);
        }

        return _bitsPerSample switch
        {
            16 => BinaryPrimitives.ReadInt16LittleEndian(sampleBytes) / (float)short.MaxValue,
            24 => ReadInt24LittleEndian(sampleBytes) / 8_388_607f,
            32 => BinaryPrimitives.ReadInt32LittleEndian(sampleBytes) / (float)int.MaxValue,
            _ => 0
        };
    }

    private static int ReadInt24LittleEndian(ReadOnlySpan<byte> bytes)
    {
        var value = bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);
        if ((value & 0x800000) != 0)
        {
            value |= unchecked((int)0xFF000000);
        }

        return value;
    }

    private void WriteStereoSample(float left, float right, out float peak)
    {
        left = Math.Clamp(left, -1f, 1f);
        right = Math.Clamp(right, -1f, 1f);
        peak = Math.Max(Math.Abs(left), Math.Abs(right));
        _pendingPeak = Math.Max(_pendingPeak, peak);

        BinaryPrimitives.WriteInt16LittleEndian(_pendingFrame.AsSpan(_pendingOffset, 2), ToPcm16(left));
        BinaryPrimitives.WriteInt16LittleEndian(_pendingFrame.AsSpan(_pendingOffset + 2, 2), ToPcm16(right));
        _pendingOffset += 4;
    }

    private void DropConsumedSourceSamples()
    {
        var consumed = Math.Max(0, (int)_sourceCursor - 2);
        if (consumed <= 0)
        {
            return;
        }

        _sourceSamples.RemoveRange(0, Math.Min(consumed, _sourceSamples.Count));
        _sourceCursor -= consumed;
    }

    private static short ToPcm16(float value)
    {
        return (short)Math.Round(Math.Clamp(value, -1f, 1f) * short.MaxValue);
    }

    private static float Lerp(float a, float b, double amount)
    {
        return (float)(a + (b - a) * amount);
    }

    private readonly record struct StereoSample(float Left, float Right);
}
