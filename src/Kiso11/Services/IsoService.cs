using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Kiso11.Services;

public class IsoService
{
    private readonly ProcessRunner _runner;

    public IsoService(ProcessRunner runner)
    {
        _runner = runner;
    }

    public async Task<string> ExtractIsoAsync(string isoPath, string destinationDirectory, Action<int, string> onProgress, CancellationToken cancellationToken = default)
    {
        onProgress(5, "Montando imagem ISO do Windows...");

        // Mount ISO using PowerShell
        var mountScript = $"$m = Mount-DiskImage -ImagePath '{isoPath}' -PassThru; ($m | Get-Volume).DriveLetter";
        var output = await _runner.RunAndCaptureOutputAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{mountScript}\"", null, cancellationToken);

        var driveMatch = Regex.Match(output, @"([A-Za-z]):?");
        if (!driveMatch.Success)
        {
            throw new InvalidOperationException("Não foi possível montar o arquivo ISO ou obter a letra da unidade.");
        }

        var sourceDrive = $"{driveMatch.Groups[1].Value.ToUpper()}:\\";

        try
        {
            onProgress(15, $"Copiando arquivos da ISO ({sourceDrive}) para diretório de trabalho...");
            Directory.CreateDirectory(destinationDirectory);

            var robocopyArgs = $"\"{sourceDrive}\" \"{destinationDirectory}\" /E /COPY:DAT /R:2 /W:2 /MT:8 /NFL /NDL /NP";
            var exitCode = await _runner.RunAsync("robocopy.exe", robocopyArgs, null, cancellationToken);
            if (exitCode > 7)
            {
                throw new InvalidOperationException($"Robocopy falhou ao copiar arquivos da ISO com código {exitCode}");
            }

            // Remove read-only attributes
            await Task.Run(() =>
            {
                var dirInfo = new DirectoryInfo(destinationDirectory);
                foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    file.Attributes &= ~FileAttributes.ReadOnly;
                }
            }, cancellationToken);

            // Handle install.esd if install.wim does not exist
            var installWim = Path.Combine(destinationDirectory, "sources", "install.wim");
            var installEsd = Path.Combine(destinationDirectory, "sources", "install.esd");

            if (!File.Exists(installWim) && File.Exists(installEsd))
            {
                onProgress(50, "Convertendo install.esd para install.wim...");
                var exportArgs = $"/Export-Image /SourceImageFile:\"{installEsd}\" /SourceIndex:1 /DestinationImageFile:\"{installWim}\" /Compress:max /CheckIntegrity";
                var dismExit = await _runner.RunAsync("dism.exe", exportArgs, null, cancellationToken);
                if (dismExit != 0)
                {
                    throw new InvalidOperationException("Falha ao converter install.esd para install.wim.");
                }
                File.Delete(installEsd);
            }

            return destinationDirectory;
        }
        finally
        {
            try
            {
                await _runner.RunAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"Dismount-DiskImage -ImagePath '{isoPath}'\"", null, CancellationToken.None);
            }
            catch { }
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

        // Search local autounattend.xml in current directory or scripts
        var localXml = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autounattend.xml");
        var scriptXml = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "autounattend.xml");
        var rootXml = Path.Combine(Directory.GetCurrentDirectory(), "autounattend.xml");

        if (File.Exists(localXml))
        {
            File.Copy(localXml, targetPath, overwrite: true);
        }
        else if (File.Exists(scriptXml))
        {
            File.Copy(scriptXml, targetPath, overwrite: true);
        }
        else if (File.Exists(rootXml))
        {
            File.Copy(rootXml, targetPath, overwrite: true);
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
            // Create empty file to bypass setup requirement check on older hardware
            File.WriteAllBytes(appraiserPath, []);
        }
        catch { }
    }
}
