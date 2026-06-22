using System.Windows;
using CableSpeaker.Core;
using CableSpeaker.Windows.Audio;
using CableSpeaker.Windows.Services;

namespace CableSpeaker.Windows;

public partial class MainWindow : Window
{
    private readonly AdbService _adbService = new();
    private CableSpeakerServer? _server;

    public MainWindow()
    {
        InitializeComponent();
        AudioDeviceText.Text = AudioDeviceService.GetDefaultRenderDeviceName();
        AdbStatusText.Text = "ADB not checked yet.";
        TunnelStatusText.Text = $"Tunnel not configured. Expected port: {ProtocolConstants.Port}.";
        AppendLog("Ready. Connect your Android phone by USB, then check the phone.");
    }

    protected override async void OnClosed(EventArgs e)
    {
        if (_server is not null)
        {
            await _server.DisposeAsync();
        }

        base.OnClosed(e);
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

            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            AppendLog($"Started on 127.0.0.1:{ProtocolConstants.Port}. Open the Android app and press Connect.");
        });
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        await StopServerAsync();
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
            var result = await _adbService.SetupReverseTunnelAsync(ProtocolConstants.Port);
            TunnelStatusText.Text = result.Summary;
            AppendLog(result.Details);
        });
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
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            AppendLog("Stopped.");
        });
    }

    private void Server_StateChanged(object? sender, ServerStateEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            LevelMeter.Value = e.State.Peak;
            AppendLogThrottled($"{e.State.Message} Frames sent: {e.State.FramesSent}. Dropped: {e.State.FramesDropped}.");
        });
    }

    private DateTime _lastThrottledLog = DateTime.MinValue;
    private string _lastThrottledMessage = "";

    private void AppendLogThrottled(string message)
    {
        if (message == _lastThrottledMessage && DateTime.UtcNow - _lastThrottledLog < TimeSpan.FromSeconds(2))
        {
            return;
        }

        _lastThrottledLog = DateTime.UtcNow;
        _lastThrottledMessage = message;
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
            StartButton.IsEnabled = _server is null;
            StopButton.IsEnabled = _server is not null;
        }
    }

    private void SetControlsEnabled(bool enabled)
    {
        StartButton.IsEnabled = enabled;
        StopButton.IsEnabled = enabled && _server is not null;
        CheckPhoneButton.IsEnabled = enabled;
        SetupTunnelButton.IsEnabled = enabled;
    }

    private void AppendLog(string message)
    {
        LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogTextBox.ScrollToEnd();
    }
}

