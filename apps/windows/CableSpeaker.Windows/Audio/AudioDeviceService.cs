using NAudio.CoreAudioApi;

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
}

