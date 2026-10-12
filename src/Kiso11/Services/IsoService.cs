using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Kiso11.Models;

namespace Kiso11.Services;

public class IsoService
{
    private readonly ProcessRunner _runner;
    private readonly DismEngineService _dism;

    public IsoService(ProcessRunner runner, DismEngineService dism)
    {
        _runner = runner;
        _dism = dism;
    }

    /// Mounts the ISO just long enough to read its editions. Nothing is copied or changed.
    public async Task<IsoInfo> InspectAsync(string isoPath, CancellationToken cancellationToken = default)
    {
        var driveRoot = await MountAsync(isoPath, cancellationToken);
        try
        {
            var (imageFile, usesEsd) = FindInstallImage(driveRoot);
            if (imageFile == null)
            {
                return new IsoInfo(false, false, Array.Empty<WimEdition>());
            }

            var editions = await _dism.GetWimEditionsAsync(imageFile, cancellationToken);
            return new IsoInfo(true, usesEsd, editions);
        }
        finally
        {
            await DismountAsync(isoPath);
        }
    }

    /// Copies the ISO into a work folder. Returns the image index to use inside the copied install.wim.
    public async Task<int> ExtractIsoAsync(
        string isoPath,
        string destinationDirectory,
        int editionIndex,
        Action<int, string> onProgress,
        CancellationToken cancellationToken = default)
    {
        var loc = LocalizationService.Instance;

        onProgress(5, loc.MsgMountingIso);
        var driveRoot = await MountAsync(isoPath, cancellationToken);
        try
        {
            onProgress(15, loc.MsgCopyingIso);
            Directory.CreateDirectory(destinationDirectory);

            var exitCode = await _runner.RunAsync(
                "robocopy.exe",
                $"\"{driveRoot}\" \"{destinationDirectory}\" /E /COPY:DAT /R:2 /W:2 /MT:8 /NFL /NDL /NP",
                null,
                cancellationToken);

            if (exitCode > 7)
            {
                throw new InvalidOperationException(string.Format(loc.ErrRobocopy, exitCode));
            }

            await Task.Run(() => ClearReadOnlyAttributes(destinationDirectory), cancellationToken);

            var installWim = Path.Combine(destinationDirectory, "sources", "install.wim");
            var installEsd = Path.Combine(destinationDirectory, "sources", "install.esd");

            if (File.Exists(installWim))
            {
                return editionIndex;
            }

            if (File.Exists(installEsd))
            {
                // Converting an ESD keeps only the chosen edition, which becomes index 1 in the new WIM.
                onProgress(50, loc.MsgConvertingEsd);
                var dismExit = await _runner.RunAsync(
                    "dism.exe",
                    $"/Export-Image /SourceImageFile:\"{installEsd}\" /SourceIndex:{editionIndex} /DestinationImageFile:\"{installWim}\" /Compress:max /CheckIntegrity",
                    null,
                    cancellationToken);

                if (dismExit != 0 || !File.Exists(installWim))
                {
                    throw new InvalidOperationException(loc.ErrEsdConvert);
                }

                File.Delete(installEsd);
                return 1;
            }

            throw new InvalidOperationException(loc.ErrNoInstallImage);
        }
        finally
        {
            await DismountAsync(isoPath);
        }
    }

    public void InjectAutounattendXml(string destinationDirectory, string? customXmlPath = null)
    {
        var targetPath = Path.Combine(destinationDirectory, "autounattend.xml");

        if (!string.IsNullOrEmpty(customXmlPath) && File.Exists(customXmlPath))
        {
            File.Copy(customXmlPath, targetPath, overwrite: true);
            return;
        }

        // The build copies autounattend.xml next to the executable, so that is the source.
        var localXml = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autounattend.xml");
        if (File.Exists(localXml))
        {
            File.Copy(localXml, targetPath, overwrite: true);
        }
    }

    public void PatchAppraiserResDll(string destinationDirectory)
    {
        var appraiserPath = Path.Combine(destinationDirectory, "sources", "appraiserres.dll");
        try
        {
            if (File.Exists(appraiserPath))
            {
                File.Delete(appraiserPath);
            }

            // An empty file stops the Setup compatibility check on unsupported hardware.
            File.WriteAllBytes(appraiserPath, []);
        }
        catch { }
    }

    /// Mounts the ISO and returns its drive root, for example "E:\".
    private async Task<string> MountAsync(string isoPath, CancellationToken cancellationToken)
    {
        // Mount-DiskImage can return before the volume is ready, so retry briefly.
        var script =
            "$ErrorActionPreference = 'Stop'\n" +
            $"$image = Mount-DiskImage -ImagePath '{EscapeForPowerShell(isoPath)}' -PassThru\n" +
            "$letter = $null\n" +
            "for ($i = 0; $i -lt 40 -and -not $letter; $i++) {\n" +
            "    $letter = ($image | Get-Volume).DriveLetter\n" +
            "    if (-not $letter) { Start-Sleep -Milliseconds 250 }\n" +
            "}\n" +
            "if ($letter) { \"$letter\" }";

        var (exitCode, output) = await _runner.PowerShellAsync(script, cancellationToken, log: false);

        var match = Regex.Match(output, @"^\s*([A-Za-z])\s*$", RegexOptions.Multiline);
        if (exitCode != 0 || !match.Success)
        {
            throw new InvalidOperationException(LocalizationService.Instance.ErrMountIso);
        }

        return $"{match.Groups[1].Value.ToUpperInvariant()}:\\";
    }

    private async Task DismountAsync(string isoPath)
    {
        var script = $"Dismount-DiskImage -ImagePath '{EscapeForPowerShell(isoPath)}' -ErrorAction SilentlyContinue | Out-Null";

        try
        {
            await _runner.PowerShellAsync(script, CancellationToken.None, log: false);
        }
        catch { }
    }

    private static (string? ImageFile, bool UsesEsd) FindInstallImage(string driveRoot)
    {
        var wim = Path.Combine(driveRoot, "sources", "install.wim");
        if (File.Exists(wim)) return (wim, false);

        var esd = Path.Combine(driveRoot, "sources", "install.esd");
        if (File.Exists(esd)) return (esd, true);

        return (null, false);
    }

    private static void ClearReadOnlyAttributes(string directory)
    {
        var dirInfo = new DirectoryInfo(directory);
        foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
        {
            file.Attributes &= ~FileAttributes.ReadOnly;
        }
    }

    private static string EscapeForPowerShell(string value) => value.Replace("'", "''");
}
