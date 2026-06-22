# CableSpeaker

CableSpeaker lets a Windows laptop stream its system audio to an Android phone over a USB cable. It can also stream the phone microphone back to Windows through VB-CABLE, so apps like Discord or Zoom can select it as a microphone.

The v1 bridge uses ADB USB tunneling:

1. The Windows app captures the default Windows output device with WASAPI loopback.
2. The Windows app listens only on `127.0.0.1:38271`.
3. `adb reverse tcp:38271 tcp:38271` exposes that local port to the phone over USB.
4. The Android app connects to `127.0.0.1:38271` and plays streamed PCM through `AudioTrack`.
5. For phone mic mode, the Windows app also listens on `127.0.0.1:38272`, and the phone sends mono PCM mic frames over a second USB tunnel.

Audio stays local between the laptop and phone. GitHub Actions are included so the Windows app and Android APK can be built in the cloud even if this laptop does not have the full SDKs installed.

## Repository layout

- `apps/windows/CableSpeaker.Core` - shared protocol, TCP server, and test audio source.
- `apps/windows/CableSpeaker.Windows` - WPF sender app using NAudio WASAPI loopback capture.
- `apps/windows/CableSpeaker.Core.Tests` - .NET protocol and reconnect tests.
- `apps/android` - native Android Kotlin receiver app.
- `tools` - PowerShell helpers for ADB platform-tools, phone checks, tunnel setup, APK install, and launch.
- `.github/workflows` - cloud builds for Windows and Android artifacts.

## First-time phone setup

1. On the Android phone, enable Developer Options.
2. Enable USB debugging.
3. Connect the phone to the laptop by USB.
4. Accept the phone's USB debugging trust prompt.
5. Run `tools\Get-PlatformTools.ps1`.
6. Run `tools\Check-Phone.ps1`.
7. Install the APK from the GitHub Actions Android artifact:

```powershell
tools\Install-AndroidApk.ps1 -ApkPath .\CableSpeaker-debug.apk
```

## Use it

1. Start the Windows app.
2. Press `Check Phone`.
3. Press `Setup USB Tunnels`.
4. Press `Start Speaker`.
5. Open CableSpeaker on the phone and press `Connect`.
6. Play audio on Windows.

Stable latency is the default because it avoids repeated buffering and broken audio.

## Use phone mic as PC microphone

1. Install VB-CABLE from the official VB-Audio page: <https://vb-audio.com/Cable/>.
2. Restart Windows if the driver installer asks.
3. Run `tools\Check-VBCable.ps1` and confirm a VB-CABLE device is detected.
4. In CableSpeaker on Windows, choose `CABLE Input` in the `Phone Mic To PC` panel.
5. Press `Start Mic Receiver`.
6. On the phone, press `Mic Start`.
7. In Discord, Zoom, Windows Sound Settings, or another app, choose `CABLE Output` as the microphone.

If VB-CABLE is not installed, CableSpeaker can receive phone mic audio, but Windows apps will not see it as a selectable microphone.

## Cloud builds

Push this folder to GitHub. The workflows will upload:

- `CableSpeaker-Windows.zip`
- `CableSpeaker-Android-Debug-Apk`

The Android workflow follows the stable SDK packages available on GitHub Actions: Gradle 9.4.1, JDK 17, compile/target SDK 36, build-tools 36.0.0, and min SDK 26.
