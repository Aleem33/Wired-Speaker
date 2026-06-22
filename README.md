# CableSpeaker

CableSpeaker lets a Windows laptop stream its system audio to an Android phone over a USB cable. It is built for the case where the laptop speakers are unavailable, but Windows audio still plays through the normal output pipeline.

The v1 bridge uses ADB USB tunneling:

1. The Windows app captures the default Windows output device with WASAPI loopback.
2. The Windows app listens only on `127.0.0.1:38271`.
3. `adb reverse tcp:38271 tcp:38271` exposes that local port to the phone over USB.
4. The Android app connects to `127.0.0.1:38271` and plays streamed PCM through `AudioTrack`.

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
3. Press `Setup USB Tunnel`.
4. Press `Start`.
5. Open CableSpeaker on the phone and press `Connect`.
6. Play audio on Windows.

If audio is delayed, choose a lower latency mode in the phone app. If it clicks or drops, choose a higher latency mode.

## Cloud builds

Push this folder to GitHub. The workflows will upload:

- `CableSpeaker-Windows.zip`
- `CableSpeaker-Android-Debug-Apk`

The Android workflow follows the AGP 9.2 compatibility table: Gradle 9.4.1, JDK 17, compile/target SDK 37, build-tools 36.0.0, and min SDK 26.
