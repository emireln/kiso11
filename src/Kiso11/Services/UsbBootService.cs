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

        // Use PowerShell Format-Volume for reliable FAT32 format
        var psCommand = $"Format-Volume -DriveLetter '{cleanLetter}' -FileSystem FAT32 -NewFileSystemLabel '{label}' -Force";
        var exitCode = await _runner.RunAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"", null, cancellationToken);

        if (exitCode != 0)
        {
            // Fallback to diskpart script
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

        return exitCode == 0;
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

        // Copy everything using Robocopy, excluding install.wim which might need splitting
        var robocopyArgs = $"\"{sourceDirectory}\" \"{usbTarget}\" /E /XF install.wim /R:2 /W:2 /MT:8 /NP /NFL /NDL";
        var roboExit = await _runner.RunAsync("robocopy.exe", robocopyArgs, sourceDirectory, cancellationToken);
        if (roboExit > 7)
        {
            throw new InvalidOperationException($"Robocopy retornou código de erro {roboExit}");
        }

        var sourceInstallWim = Path.Combine(sourceDirectory, "sources", "install.wim");
        if (!File.Exists(sourceInstallWim))
        {
            throw new FileNotFoundException("install.wim não encontrado no diretório de origem.");
        }

        var wimFileInfo = new FileInfo(sourceInstallWim);
        const long fat32MaxSingleFile = 4294967295L; // 4GB - 1 byte

        if (wimFileInfo.Length >= fat32MaxSingleFile)
        {
            onProgress(60, "Arquivo install.wim > 4GB. Dividindo em partes SWM para compatibilidade FAT32 UEFI...");
            var targetSwm = Path.Combine(targetSources, "install.swm");
            var dismSplitArgs = $"/Split-Image /ImageFile:\"{sourceInstallWim}\" /SWMFile:\"{targetSwm}\" /FileSize:3800";
            var dismExit = await _runner.RunAsync("dism.exe", dismSplitArgs, null, cancellationToken);

            if (dismExit != 0)
            {
                throw new InvalidOperationException($"Falha ao dividir o arquivo install.wim com DISM (código {dismExit}).");
            }
        }
        else
        {
            onProgress(60, "Copiando install.wim para o pendrive...");
            var targetWim = Path.Combine(targetSources, "install.wim");
            await Task.Run(() => File.Copy(sourceInstallWim, targetWim, overwrite: true), cancellationToken);
        }

        onProgress(95, "Configurando setor de inicialização de boot...");
        var bootsectPath = Path.Combine(sourceDirectory, "boot", "bootsect.exe");
        if (File.Exists(bootsectPath))
        {
            await _runner.RunAsync(bootsectPath, $"/nt60 {cleanLetter} /force", null, cancellationToken);
        }

        onProgress(100, "Pendrive bootável criado com sucesso!");
    }
}
