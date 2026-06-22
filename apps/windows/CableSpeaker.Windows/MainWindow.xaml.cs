using System.Windows;
using CableSpeaker.Core;
using CableSpeaker.Windows.Audio;
using CableSpeaker.Windows.Services;

namespace CableSpeaker.Windows;

public partial class MainWindow : Window
{
    private readonly AdbService _adbService = new();
    private readonly AppSettings _settings;
    private CableSpeakerServer? _server;
    private MicReceiverService? _micReceiver;
    private DateTime _lastSpeakerLog = DateTime.MinValue;
    private DateTime _lastMicLog = DateTime.MinValue;
    private string _lastSpeakerMessage = "";
    private string _lastMicMessage = "";

    public MainWindow()
    {
        InitializeComponent();
        _settings = AppSettings.Load();
        AudioDeviceText.Text = AudioDeviceService.GetDefaultRenderDeviceName();
        AdbStatusText.Text = "ADB not checked yet.";
        TunnelStatusText.Text = $"Tunnels not configured. Expected ports: {ProtocolConstants.SpeakerPort} and {ProtocolConstants.MicPort}.";
        LoadMicOutputDevices();
        AppendLog("Ready. Connect your Android phone by USB, then set up tunnels.");
    }

    protected override async void OnClosed(EventArgs e)
    {
        if (_server is not null)
        {
            await _server.DisposeAsync();
        }

        if (_micReceiver is not null)
        {
            await _micReceiver.DisposeAsync();
        }

        base.OnClosed(e);
    }

    private void LoadMicOutputDevices()
    {
        var devices = AudioDeviceService.GetWaveOutputDevices();
        MicOutputComboBox.ItemsSource = devices;
        MicOutputComboBox.DisplayMemberPath = nameof(WaveOutputDeviceInfo.Name);
        MicOutputComboBox.SelectedValuePath = nameof(WaveOutputDeviceInfo.Name);

        var selected = AudioDeviceService.FindPreferredMicOutputDevice(_settings.MicOutputDeviceName);
        MicOutputComboBox.SelectedItem = selected;

        VbCableStatusText.Text = AudioDeviceService.HasVbCable()
            ? "VB-CABLE detected. Choose CABLE Input here, then CABLE Output in your PC app."
            : "VB-CABLE not detected. Install it from vb-audio.com/Cable for a real selectable PC microphone.";
        MicStatsText.Text = "Mic receiver stopped.";
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(async () =>
        {
            if (_server is not null)
            {
                return;
            }

            AudioDeviceText.Text = AudioDeviceService.GetDefaultRenderDeviceName();
            var source = new WasapiLoopbackFrameSource();
            _server = new CableSpeakerServer(source);
            _server.StateChanged += Server_StateChanged;
            await _server.StartAsync();
            AppendLog($"Speaker stream started on 127.0.0.1:{ProtocolConstants.SpeakerPort}. Press Connect on the phone.");
        });
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        await StopServerAsync();
    }

    private async void StartMicButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(async () =>
        {
            if (_micReceiver is not null)
            {
                return;
            }

            if (MicOutputComboBox.SelectedItem is not WaveOutputDeviceInfo device)
            {
                throw new InvalidOperationException("Select a Windows output device for the phone mic first.");
            }

            _settings.MicOutputDeviceName = device.Name;
            _settings.Save();

            _micReceiver = new MicReceiverService(device.DeviceNumber);
            _micReceiver.StateChanged += MicReceiver_StateChanged;
            await _micReceiver.StartAsync();
            AppendLog($"Mic receiver started on 127.0.0.1:{ProtocolConstants.MicPort}. Press Mic Start on the phone.");
        });
    }

    private async void StopMicButton_Click(object sender, RoutedEventArgs e)
    {
        await StopMicReceiverAsync();
    }

    private async void CheckPhoneButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(async () =>
        {
            var result = await _adbService.GetDeviceStatusAsync();
            AdbStatusText.Text = result.Summary;
            AppendLog(result.Details);
        });
    }

    private async void SetupTunnelButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(async () =>
        {
            var result = await _adbService.SetupCableSpeakerTunnelsAsync();
            TunnelStatusText.Text = result.Summary;
            AppendLog(result.Details);
        });
    }

    private void MicOutputComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (MicOutputComboBox.SelectedItem is not WaveOutputDeviceInfo device)
        {
            return;
        }

        _settings.MicOutputDeviceName = device.Name;
        _settings.Save();
    }

    private async Task StopServerAsync()
    {
        await RunUiActionAsync(async () =>
        {
            if (_server is null)
            {
                return;
            }

            _server.StateChanged -= Server_StateChanged;
            await _server.DisposeAsync();
            _server = null;
            LevelMeter.Value = 0;
            AppendLog("Speaker stream stopped.");
        });
    }

    private async Task StopMicReceiverAsync()
    {
        await RunUiActionAsync(async () =>
        {
            if (_micReceiver is null)
            {
                return;
            }

            _micReceiver.StateChanged -= MicReceiver_StateChanged;
            await _micReceiver.DisposeAsync();
            _micReceiver = null;
            MicLevelMeter.Value = 0;
            MicStatsText.Text = "Mic receiver stopped.";
            AppendLog("Mic receiver stopped.");
        });
    }

    private void Server_StateChanged(object? sender, ServerStateEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            LevelMeter.Value = e.State.Peak;
            AppendSpeakerLogThrottled($"{e.State.Message} Frames sent: {e.State.FramesSent}. Dropped: {e.State.FramesDropped}.");
        });
    }

    private void MicReceiver_StateChanged(object? sender, MicReceiverStateEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            MicLevelMeter.Value = e.State.Peak;
            MicStatsText.Text = $"{e.State.Message} Frames: {e.State.FramesReceived}. Dropped: {e.State.FramesDropped}. Buffer: {e.State.BufferedMilliseconds}ms.";
            AppendMicLogThrottled(MicStatsText.Text);
        });
    }

    private void AppendSpeakerLogThrottled(string message)
    {
        if (message == _lastSpeakerMessage && DateTime.UtcNow - _lastSpeakerLog < TimeSpan.FromSeconds(2))
        {
            return;
        }

        _lastSpeakerLog = DateTime.UtcNow;
        _lastSpeakerMessage = message;
        AppendLog(message);
    }

    private void AppendMicLogThrottled(string message)
    {
        if (message == _lastMicMessage && DateTime.UtcNow - _lastMicLog < TimeSpan.FromSeconds(2))
        {
            return;
        }

        _lastMicLog = DateTime.UtcNow;
        _lastMicMessage = message;
        AppendLog(message);
    }

    private async Task RunUiActionAsync(Func<Task> action)
    {
        SetControlsEnabled(false);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            AppendLog($"Error: {ex.Message}");
            MessageBox.Show(this, ex.Message, "CableSpeaker", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    private void SetControlsEnabled(bool enabled)
    {
        CheckPhoneButton.IsEnabled = enabled;
        SetupTunnelButton.IsEnabled = enabled;
        StartButton.IsEnabled = enabled && _server is null;
        StopButton.IsEnabled = enabled && _server is not null;
        StartMicButton.IsEnabled = enabled && _micReceiver is null;
        StopMicButton.IsEnabled = enabled && _micReceiver is not null;
        MicOutputComboBox.IsEnabled = enabled && _micReceiver is null;
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogTextBox.ScrollToEnd();
    }
}
