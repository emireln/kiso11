using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Kiso11.Models;

namespace Kiso11.Services;

public sealed class IsoService
{
    private readonly ProcessRunner _runner;
    private readonly DismEngineService _dismService;

    public IsoService(ProcessRunner runner, DismEngineService dismService)
    {
        _runner = runner;
        _dismService = dismService;
    }

    public async Task<(string ImagePath, System.Collections.Generic.List<WindowsImageEdition> Editions)> InspectIsoAsync(
        string isoPath,
        CancellationToken cancellationToken = default)
    {
        var driveLetter = await MountIsoAsync(isoPath, cancellationToken);
        try
        {
            var sourcesPath = Path.Combine(driveLetter + @"\", "sources");
            var imagePath = FindInstallImage(sourcesPath);
            if (imagePath == null)
            {
                throw new FileNotFoundException("A ISO não contém sources\\install.wim, install.esd ou install.swm.");
            }
            ValidateBootFiles(Path.GetDirectoryName(sourcesPath)!);

            var editions = await _dismService.GetWimEditionsAsync(imagePath, cancellationToken);
            if (editions.Count == 0)
            {
                throw new InvalidOperationException("O DISM não encontrou edições válidas na imagem de instalação.");
            }

            return (Path.GetFileName(imagePath), editions);
        }
        finally
        {
            await DismountIsoAsync(isoPath);
        }
    }

    public async Task<string> ExtractIsoAsync(
        string isoPath,
        string destinationDirectory,
        Action<int, string> onProgress,
        CancellationToken cancellationToken = default)
    {
        onProgress(5, "Mounting Windows ISO...");
        var driveLetter = await MountIsoAsync(isoPath, cancellationToken);
        var sourceDrive = driveLetter + @"\";

        try
        {
            onProgress(15, $"Copying ISO files from {sourceDrive}...");
            Directory.CreateDirectory(destinationDirectory);

            var robocopyArgs = $"\"{sourceDrive}\" \"{destinationDirectory}\" /E /COPY:DAT /R:2 /W:2 /MT:8 /NFL /NDL /NP";
            var exitCode = await _runner.RunAsync("robocopy.exe", robocopyArgs, null, cancellationToken);
            if (exitCode > 7)
            {
                throw new InvalidOperationException($"Robocopy failed to copy the ISO (exit code {exitCode}).");
            }

            await Task.Run(() =>
            {
                foreach (var file in new DirectoryInfo(destinationDirectory).GetFiles("*", SearchOption.AllDirectories))
                {
                    file.Attributes &= ~FileAttributes.ReadOnly;
                }
            }, cancellationToken);

            var sourcesPath = Path.Combine(destinationDirectory, "sources");
            if (FindInstallImage(sourcesPath) == null)
            {
                throw new FileNotFoundException("No supported install.wim, install.esd, or install.swm was found in the ISO.");
            }
            ValidateBootFiles(destinationDirectory);

            return destinationDirectory;
        }
        finally
        {
            await DismountIsoAsync(isoPath);
        }
    }

    private async Task<string> MountIsoAsync(string isoPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(isoPath)) throw new FileNotFoundException("The selected ISO file no longer exists.", isoPath);

        var pathLiteral = "'" + isoPath.Replace("'", "''") + "'";
        var script = "$image = Mount-DiskImage -ImagePath " + pathLiteral + " -PassThru -ErrorAction Stop; $image | Get-Volume -ErrorAction Stop | Select-Object -ExpandProperty DriveLetter";
        var output = await _runner.RunAndCaptureOutputAsync("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"", null, cancellationToken);
        var driveMatch = Regex.Match(output, @"(?m)^\s*([A-Za-z])\s*$");
        if (!driveMatch.Success)
        {
            await DismountIsoAsync(isoPath);
            throw new InvalidOperationException("Windows could not mount the ISO or assign it a drive letter.");
        }

        return driveMatch.Groups[1].Value.ToUpperInvariant() + ":";
    }

    private async Task DismountIsoAsync(string isoPath)
    {
        try
        {
            var pathLiteral = "'" + isoPath.Replace("'", "''") + "'";
            var script = "Dismount-DiskImage -ImagePath " + pathLiteral + " -ErrorAction SilentlyContinue | Out-Null";
            await _runner.RunAsync("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"", null, CancellationToken.None);
        }
        catch { }
    }

    private static string? FindInstallImage(string sourcesPath)
    {
        foreach (var fileName in new[] { "install.wim", "install.esd", "install.swm" })
        {
            var candidate = Path.Combine(sourcesPath, fileName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static void ValidateBootFiles(string isoRoot)
    {
        var requiredFiles = new[]
        {
            Path.Combine(isoRoot, "sources", "boot.wim"),
            Path.Combine(isoRoot, "boot", "etfsboot.com"),
            Path.Combine(isoRoot, "efi", "microsoft", "boot", "efisys.bin")
        };
        var missing = requiredFiles.Where(path => !File.Exists(path)).Select(path => Path.GetRelativePath(isoRoot, path)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidDataException("The ISO is missing boot files required to create installation media: " + string.Join(", ", missing));
        }
    }

    public void InjectAutounattendXml(
        string destinationDirectory,
        string? customXmlPath = null,
        bool includeHardwareBypass = true,
        bool disableAutomaticEncryption = true)
    {
        var targetPath = Path.Combine(destinationDirectory, "autounattend.xml");
        var candidatePaths = new[]
        {
            customXmlPath,
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autounattend.xml"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "autounattend.xml"),
            Path.Combine(Directory.GetCurrentDirectory(), "autounattend.xml")
        };

        var sourcePath = candidatePaths.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
        if (sourcePath == null)
        {
            throw new FileNotFoundException("The bundled autounattend.xml file is missing.");
        }

        if (includeHardwareBypass && disableAutomaticEncryption)
        {
            File.Copy(sourcePath, targetPath, overwrite: true);
            return;
        }

        var document = XDocument.Load(sourcePath);
        XNamespace ns = "urn:schemas-microsoft-com:unattend";
        foreach (var command in document.Descendants(ns + "RunSynchronousCommand")
                     .Where(command =>
                     {
                         var path = command.Element(ns + "Path")?.Value ?? string.Empty;
                         return (!includeHardwareBypass && path.Contains("Setup\\LabConfig", StringComparison.OrdinalIgnoreCase))
                             || (!disableAutomaticEncryption && path.Contains("Control\\BitLocker", StringComparison.OrdinalIgnoreCase));
                     }))
        {
            command.Remove();
        }
        document.Save(targetPath);
    }
}
