using System.Diagnostics;
using System.IO;
using CableSpeaker.Core;

namespace CableSpeaker.Windows.Services;

public sealed class AdbService
{
    public async Task<AdbCommandResult> GetDeviceStatusAsync()
    {
        var adb = FindAdb();
        if (adb is null)
        {
            return new AdbCommandResult(
                false,
                "ADB not found.",
                "ADB was not found. Run tools\\Get-PlatformTools.ps1, then try again.");
        }

        var result = await RunAdbAsync(adb, "devices");
        var deviceLines = result.Output
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .Where(line => line.Contains('\t'))
            .ToArray();

        if (deviceLines.Length == 0)
        {
            return new AdbCommandResult(false, "No Android phone detected.", result.Output.Trim());
        }

        var authorized = deviceLines.FirstOrDefault(line => line.EndsWith("\tdevice", StringComparison.OrdinalIgnoreCase));
        if (authorized is null)
        {
            return new AdbCommandResult(
                false,
                "Phone detected but not authorized.",
                result.Output.Trim() + Environment.NewLine + "Unlock the phone and accept the USB debugging prompt.");
        }

        return new AdbCommandResult(true, "Phone connected and authorized.", result.Output.Trim());
    }

    public async Task<AdbCommandResult> SetupReverseTunnelAsync(int port = ProtocolConstants.Port)
    {
        var adb = FindAdb();
        if (adb is null)
        {
            return new AdbCommandResult(
                false,
                "ADB not found.",
                "ADB was not found. Run tools\\Get-PlatformTools.ps1, then try again.");
        }

        var devices = await GetDeviceStatusAsync();
        if (!devices.Success)
        {
            return devices;
        }

        var args = $"reverse tcp:{port} tcp:{port}";
        var result = await RunAdbAsync(adb, args);
        if (result.ExitCode != 0)
        {
            return new AdbCommandResult(false, "USB tunnel setup failed.", result.Output.Trim());
        }

        return new AdbCommandResult(
            true,
            $"USB tunnel ready on tcp:{port}.",
            $"adb {args}{Environment.NewLine}{result.Output.Trim()}");
    }

    private static string? FindAdb()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "platform-tools", "adb.exe"),
            Path.Combine(baseDir, "tools", "platform-tools", "adb.exe"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "tools", "platform-tools", "adb.exe")),
            Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "tools", "platform-tools", "adb.exe"))
        };

        foreach (var candidate in candidates)
        {
            if (candidate.Equals("adb.exe", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return FindOnPath("adb.exe");
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var folder in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(folder.Trim(), fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static async Task<(int ExitCode, string Output)> RunAdbAsync(string adbPath, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = adbPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start adb.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();
        var output = await outputTask + await errorTask;
        return (process.ExitCode, output);
    }
}
