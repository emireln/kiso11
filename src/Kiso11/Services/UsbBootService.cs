using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Kiso11.Models;

namespace Kiso11.Services;

public class UsbBootService
{
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
            var systemDrive = (Environment.GetEnvironmentVariable("SystemDrive") ?? "C:").TrimEnd('\\');

            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Removable)
                .ToList();

            foreach (var d in drives)
            {
                var letter = d.Name.TrimEnd('\\');
                if (letter.Equals(systemDrive, StringComparison.OrdinalIgnoreCase)) continue;

                var label = string.IsNullOrWhiteSpace(d.VolumeLabel) ? "USB Drive" : d.VolumeLabel;
                result.Add(new UsbDriveItem
                {
                    DriveLetter = letter,
                    VolumeLabel = label,
                    TotalBytes = d.TotalSize,
                    FreeBytes = d.TotalFreeSpace,
                    Model = "Disco Removível USB"
                });
            }
        }
        catch
        {
            // Fallback silently without blocking the UI
        }

        return result;
    }

    public async Task<bool> FormatUsbDriveAsync(string driveLetter, string label = "KISO11", CancellationToken cancellationToken = default)
    {
        var cleanLetter = driveLetter.TrimEnd('\\', ':');
        var systemDrive = (Environment.GetEnvironmentVariable("SystemDrive") ?? "C:").TrimEnd('\\', ':');

        if (cleanLetter.Equals(systemDrive, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Tentativa bloqueada de formatar o drive do sistema!");
        }

        // Try formatting the existing partition first.
        var psCommand = $"try {{ Format-Volume -DriveLetter '{cleanLetter}' -FileSystem FAT32 -NewFileSystemLabel '{label}' -Force -ErrorAction Stop | Out-Null; $v = Get-Volume -DriveLetter '{cleanLetter}' -ErrorAction Stop; if ($v.FileSystem -ne 'FAT32') {{ exit 1 }} }} catch {{ exit 1 }}";
        var exitCode = await _runner.RunAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"", null, cancellationToken);

        if (exitCode != 0)
        {
            var diskInfoScript = "$p = Get-Partition -DriveLetter '" + cleanLetter + "' -ErrorAction Stop | Select-Object -First 1; $d = Get-Disk -Number $p.DiskNumber -ErrorAction Stop; '{0}|{1}|{2}|{3}' -f $d.Number,$d.IsBoot,$d.IsSystem,$d.Size";
            var diskInfo = await _runner.RunAndCaptureOutputAsync("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{diskInfoScript}\"", null, cancellationToken);
            var match = Regex.Match(diskInfo, @"(?m)^\s*(\d+)\|(True|False)\|(True|False)\|(\d+)\s*$", RegexOptions.IgnoreCase);

            if (match.Success && (bool.Parse(match.Groups[2].Value) || bool.Parse(match.Groups[3].Value)))
            {
                throw new InvalidOperationException("Formatting was blocked because the selected USB belongs to a boot or system disk.");
            }

            if (match.Success && long.TryParse(match.Groups[4].Value, out var diskSize) && diskSize > 32L * 1024 * 1024 * 1024)
            {
                // Windows cannot format a single FAT32 volume larger than 32 GB. Recreate a 32 GB UEFI boot partition.
                var diskNumber = int.Parse(match.Groups[1].Value);
                var diskpartScript = Path.Combine(Path.GetTempPath(), $"kiso11_format_{Guid.NewGuid():N}.txt");
                var commands = $"select disk {diskNumber}\nclean\nconvert mbr\ncreate partition primary size=32768\nformat fs=fat32 quick label={label}\nassign letter={cleanLetter}\nexit\n";
                await File.WriteAllTextAsync(diskpartScript, commands, cancellationToken);
                try
                {
                    exitCode = await _runner.RunAsync("diskpart.exe", $"/s \"{diskpartScript}\"", null, cancellationToken);
                }
                finally
                {
                    if (File.Exists(diskpartScript)) File.Delete(diskpartScript);
                }
            }
            else
            {
                // Fallback for normal-sized removable volumes.
                var diskpartScript = Path.Combine(Path.GetTempPath(), $"kiso11_format_{Guid.NewGuid():N}.txt");
                await File.WriteAllTextAsync(diskpartScript, $"select volume {cleanLetter}\nformat fs=fat32 quick label={label}\nassign letter={cleanLetter}\nexit\n", cancellationToken);
                try
                {
                    exitCode = await _runner.RunAsync("diskpart.exe", $"/s \"{diskpartScript}\"", null, cancellationToken);
                }
                finally
                {
                    if (File.Exists(diskpartScript)) File.Delete(diskpartScript);
                }
            }
        }

        if (exitCode != 0) return false;
        try
        {
            var formattedDrive = new DriveInfo(cleanLetter + @":\");
            return formattedDrive.IsReady && formattedDrive.DriveFormat.Equals("FAT32", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public async Task CreateBootableUsbAsync(
        string sourceDirectory,
        string usbDriveLetter,
        Action<int, string> onProgress,
        CancellationToken cancellationToken = default)
    {
        var cleanLetter = usbDriveLetter.TrimEnd('\\');
        var usbTarget = $"{cleanLetter}\\";

        onProgress(5, "Formatando pendrive para FAT32 (compatível com UEFI)...");
        var formatted = await FormatUsbDriveAsync(cleanLetter, "KISO11", cancellationToken);
        if (!formatted)
        {
            throw new InvalidOperationException($"Não foi possível formatar o pendrive {cleanLetter}. Verifique se ele está em uso.");
        }

        onProgress(15, "Copiando arquivos de inicialização e setup para o pendrive...");

        // Ensure target directories exist
        var targetSources = Path.Combine(usbTarget, "sources");
        Directory.CreateDirectory(targetSources);

        // Copy everything except the installation payload, which is handled below for FAT32.
        var robocopyArgs = $"\"{sourceDirectory}\" \"{usbTarget}\" /E /XF install.wim install.esd install*.swm /R:2 /W:2 /MT:8 /NP /NFL /NDL";
        var roboExit = await _runner.RunAsync("robocopy.exe", robocopyArgs, sourceDirectory, cancellationToken);
        if (roboExit > 7)
        {
            throw new InvalidOperationException($"Robocopy retornou código de erro {roboExit}");
        }

        var sourceInstallWim = Path.Combine(sourceDirectory, "sources", "install.wim");
        var sourceInstallEsd = Path.Combine(sourceDirectory, "sources", "install.esd");
        var workingWim = sourceInstallWim;
        var temporaryWim = Path.Combine(sourceDirectory, "sources", "install_usb.wim");
        if (!File.Exists(workingWim) && File.Exists(sourceInstallEsd))
        {
            var esdInfo = new FileInfo(sourceInstallEsd);
            if (esdInfo.Length < 4294967295L)
            {
                onProgress(65, "Copying the compressed install.esd to the USB drive...");
                await Task.Run(() => File.Copy(sourceInstallEsd, Path.Combine(targetSources, "install.esd"), overwrite: true), cancellationToken);
                onProgress(100, "Bootable USB drive created successfully.");
                return;
            }

            onProgress(55, "The compressed image is too large for FAT32. Creating a split-ready WIM...");
            var exportExit = await _runner.RunAsync(WindowsToolLocator.FindDism(), $"/Export-Image /SourceImageFile:\"{sourceInstallEsd}\" /SourceIndex:1 /DestinationImageFile:\"{temporaryWim}\" /Compress:max /CheckIntegrity", null, cancellationToken);
            if (exportExit != 0 || !File.Exists(temporaryWim))
            {
                throw new InvalidOperationException($"DISM could not prepare the large ESD image for FAT32 (exit code {exportExit}).");
            }
            workingWim = temporaryWim;
        }

        if (!File.Exists(workingWim))
        {
            throw new FileNotFoundException("No install.wim or install.esd was found in the working image.");
        }

        var wimFileInfo = new FileInfo(workingWim);
        const long fat32MaxSingleFile = 4294967295L; // 4GB - 1 byte

        if (wimFileInfo.Length >= fat32MaxSingleFile)
        {
            onProgress(60, "Arquivo install.wim > 4GB. Dividindo em partes SWM para compatibilidade FAT32 UEFI...");
            var targetSwm = Path.Combine(targetSources, "install.swm");
            var dismSplitArgs = $"/Split-Image /ImageFile:\"{workingWim}\" /SWMFile:\"{targetSwm}\" /FileSize:3800 /CheckIntegrity";
            var dismExit = await _runner.RunAsync(WindowsToolLocator.FindDism(), dismSplitArgs, null, cancellationToken);

            if (dismExit != 0)
            {
                throw new InvalidOperationException($"Falha ao dividir o arquivo install.wim com DISM (código {dismExit}).");
            }
        }
        else
        {
            onProgress(60, "Copiando install.wim para o pendrive...");
            var sourceFileName = Path.GetFileName(workingWim);
            var targetImage = Path.Combine(targetSources, sourceFileName == "install.esd" ? "install.esd" : "install.wim");
            await Task.Run(() => File.Copy(workingWim, targetImage, overwrite: true), cancellationToken);
        }

        if (File.Exists(temporaryWim)) File.Delete(temporaryWim);

        onProgress(95, "Configurando setor de inicialização de boot...");
        var bootsectPath = Path.Combine(sourceDirectory, "boot", "bootsect.exe");
        if (File.Exists(bootsectPath))
        {
            await _runner.RunAsync(bootsectPath, $"/nt60 {cleanLetter} /force", null, cancellationToken);
        }

        onProgress(100, "Pendrive bootável criado com sucesso!");
    }
}
