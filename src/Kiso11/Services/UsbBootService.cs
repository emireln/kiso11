using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kiso11.Models;

namespace Kiso11.Services;

public class UsbBootService
{
    private const string VolumeLabel = "KISO11";
    private const long Fat32MaxSingleFileBytes = 4294967295L; // 4 GB - 1 byte

    private readonly ProcessRunner _runner;

    public UsbBootService(ProcessRunner runner)
    {
        _runner = runner;
    }

    public List<UsbDriveItem> GetAvailableUsbDrives()
    {
        var result = new List<UsbDriveItem>();

        try
        {
            var systemDrive = SystemDriveLetter();

            foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Removable))
            {
                var letter = d.Name.TrimEnd('\\');
                if (letter.Equals(systemDrive + ":", StringComparison.OrdinalIgnoreCase)) continue;

                result.Add(new UsbDriveItem
                {
                    DriveLetter = letter,
                    VolumeLabel = d.VolumeLabel,
                    TotalBytes = d.TotalSize,
                    FreeBytes = d.TotalFreeSpace
                });
            }
        }
        catch
        {
            // A failed drive scan leaves the list empty instead of blocking the UI.
        }

        return result;
    }

    public async Task FormatUsbDriveAsync(string driveLetter, CancellationToken cancellationToken = default)
    {
        var letter = ValidateTarget(driveLetter);

        var script = $"Format-Volume -DriveLetter '{letter}' -FileSystem FAT32 -NewFileSystemLabel '{VolumeLabel}' -Force -Confirm:$false | Out-Null";
        var (exitCode, _) = await _runner.PowerShellAsync(script, cancellationToken);
        if (exitCode == 0) return;

        // Format-Volume can fail on a busy volume, so retry with diskpart.
        var diskpartScript = Path.Combine(Path.GetTempPath(), $"kiso11_format_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(diskpartScript, $"select volume {letter}\nformat fs=fat32 quick label={VolumeLabel}\nassign letter={letter}\nexit\n", cancellationToken);
        try
        {
            exitCode = await _runner.RunAsync("diskpart.exe", $"/s \"{diskpartScript}\"", null, cancellationToken);
        }
        finally
        {
            if (File.Exists(diskpartScript)) File.Delete(diskpartScript);
        }

        if (exitCode != 0)
        {
            throw new InvalidOperationException(string.Format(LocalizationService.Instance.ErrFormatUsb, letter));
        }
    }

    public async Task CreateBootableUsbAsync(
        string sourceDirectory,
        string usbDriveLetter,
        Action<int, string> onProgress,
        CancellationToken cancellationToken = default)
    {
        var loc = LocalizationService.Instance;
        var letter = ValidateTarget(usbDriveLetter);
        var usbTarget = $"{letter}\\";

        onProgress(5, loc.MsgFormattingUsb);
        await FormatUsbDriveAsync(letter, cancellationToken);

        onProgress(15, loc.MsgCopyingBootFiles);
        var targetSources = Path.Combine(usbTarget, "sources");
        Directory.CreateDirectory(targetSources);

        // install.wim is copied separately because it may need splitting.
        var robocopyArgs = $"\"{sourceDirectory}\" \"{usbTarget}\" /E /XF install.wim /R:2 /W:2 /MT:8 /NP /NFL /NDL";
        var roboExit = await _runner.RunAsync("robocopy.exe", robocopyArgs, sourceDirectory, cancellationToken);
        if (roboExit > 7)
        {
            throw new InvalidOperationException(string.Format(loc.ErrRobocopy, roboExit));
        }

        var sourceInstallWim = Path.Combine(sourceDirectory, "sources", "install.wim");
        if (!File.Exists(sourceInstallWim))
        {
            throw new FileNotFoundException(loc.ErrInstallWimMissing);
        }

        if (new FileInfo(sourceInstallWim).Length >= Fat32MaxSingleFileBytes)
        {
            onProgress(60, loc.MsgSplittingWim);
            var targetSwm = Path.Combine(targetSources, "install.swm");
            var dismExit = await _runner.RunAsync(
                "dism.exe",
                $"/Split-Image /ImageFile:\"{sourceInstallWim}\" /SWMFile:\"{targetSwm}\" /FileSize:3800",
                null,
                cancellationToken);

            if (dismExit != 0)
            {
                throw new InvalidOperationException(string.Format(loc.ErrSplitWim, dismExit));
            }
        }
        else
        {
            onProgress(60, loc.MsgCopyingWim);
            var targetWim = Path.Combine(targetSources, "install.wim");
            await Task.Run(() => File.Copy(sourceInstallWim, targetWim, overwrite: true), cancellationToken);
        }

        onProgress(95, loc.MsgWritingBootSector);
        var bootsectPath = Path.Combine(sourceDirectory, "boot", "bootsect.exe");
        if (File.Exists(bootsectPath))
        {
            await _runner.RunAsync(bootsectPath, $"/nt60 {letter}: /force", null, cancellationToken);
        }

        onProgress(100, loc.MsgUsbReady);
    }

    /// Checks that a drive is safe to format before anything is erased.
    private static string ValidateTarget(string driveLetter)
    {
        var loc = LocalizationService.Instance;
        var letter = driveLetter.Trim().TrimEnd('\\', ':').ToUpperInvariant();

        if (letter.Length != 1 || !char.IsLetter(letter[0]))
        {
            throw new ArgumentException(loc.ErrInvalidDrive, nameof(driveLetter));
        }

        if (letter.Equals(SystemDriveLetter(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(loc.ErrSystemDrive);
        }

        var info = new DriveInfo(letter + ":\\");
        if (info.DriveType != DriveType.Removable)
        {
            throw new InvalidOperationException(string.Format(loc.ErrNotRemovable, letter));
        }

        if (info.TotalSize > UsbDriveItem.Fat32FormatLimitBytes)
        {
            throw new InvalidOperationException(string.Format(loc.ErrUsbTooLarge, letter));
        }

        return letter;
    }

    private static string SystemDriveLetter()
    {
        var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
        return systemDrive.TrimEnd('\\', ':').ToUpperInvariant();
    }
}
