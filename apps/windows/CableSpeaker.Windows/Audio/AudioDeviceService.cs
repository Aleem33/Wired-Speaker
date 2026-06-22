using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CableSpeaker.Windows.Audio;

public static class AudioDeviceService
{
    public static string GetDefaultRenderDeviceName()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            if (device.State != DeviceState.Active)
            {
                return $"Default render device is not active: {device.FriendlyName}";
            }

            return device.FriendlyName;
        }
        catch (Exception ex)
        {
            return $"No active Windows audio output device found: {ex.Message}";
        }
    }

    public static IReadOnlyList<WaveOutputDeviceInfo> GetWaveOutputDevices()
    {
        var devices = new List<WaveOutputDeviceInfo>();
        for (var i = 0; i < WaveOut.DeviceCount; i++)
        {
            var caps = WaveOut.GetCapabilities(i);
            devices.Add(new WaveOutputDeviceInfo(i, caps.ProductName));
        }

        return devices;
    }

    public static WaveOutputDeviceInfo? FindPreferredMicOutputDevice(string? savedName)
    {
        var devices = GetWaveOutputDevices();
        if (!string.IsNullOrWhiteSpace(savedName))
        {
            var saved = devices.FirstOrDefault(device =>
                device.Name.Equals(savedName, StringComparison.OrdinalIgnoreCase));
            if (saved is not null)
            {
                return saved;
            }
        }

        return devices.FirstOrDefault(device =>
                   device.Name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase) ||
                   device.Name.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase))
               ?? devices.FirstOrDefault();
    }

    public static bool HasVbCable()
    {
        return GetWaveOutputDevices().Any(device =>
            device.Name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase) ||
            device.Name.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record WaveOutputDeviceInfo(int DeviceNumber, string Name)
{
    public override string ToString() => Name;
}
