using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Kiso11.Services;

public class OscdimgService
{
    private readonly ProcessRunner _runner;

    public OscdimgService(ProcessRunner runner)
    {
        _runner = runner;
    }

    public async Task<string> EnsureOscdimgPathAsync(CancellationToken cancellationToken = default)
    {
        // 1. Check local tools directory
        var localToolDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools");
        var localOscdimg = Path.Combine(localToolDir, "oscdimg.exe");
        if (File.Exists(localOscdimg)) return localOscdimg;

        // 2. Check standard Windows ADK locations
        var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
        var adkOscdimg = Path.Combine(systemDrive, @"Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe");
        if (File.Exists(adkOscdimg)) return adkOscdimg;

        var adkOscdimg11 = Path.Combine(systemDrive, @"Program Files (x86)\Windows Kits\11\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe");
        if (File.Exists(adkOscdimg11)) return adkOscdimg11;

        // 3. Check AppData local
        var appDataOscdimg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kiso11", "oscdimg.exe");
        if (File.Exists(appDataOscdimg)) return appDataOscdimg;

        // 4. Download from Microsoft ADK repository
        Directory.CreateDirectory(Path.GetDirectoryName(appDataOscdimg)!);
        await DownloadOscdimgAsync(appDataOscdimg, cancellationToken);

        return appDataOscdimg;
    }

    private async Task DownloadOscdimgAsync(string destinationPath, CancellationToken cancellationToken)
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "Kiso11_ADK_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempFolder);

        try
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromMinutes(2);

            // Fetch redirect location for Windows ADK
            var response = await client.GetAsync("https://go.microsoft.com/fwlink/?linkid=2290227", cancellationToken);
            string? baseDownloadUrl = null;

            if (response.Headers.Location != null)
            {
                baseDownloadUrl = response.Headers.Location.ToString().TrimEnd('/') + "/";
            }
            else
            {
                baseDownloadUrl = "https://download.microsoft.com/download/2/d/9/2d9c8902-3fcd-48a6-a22a-432b08bed61e/ADK/";
            }

            var cabFileName = "5d984200acbde182fd99cbfbe9bad133.cab";
            var cabUrl = $"{baseDownloadUrl}Installers/{cabFileName}";
            var cabFilePath = Path.Combine(tempFolder, cabFileName);

            using var downloadClient = new HttpClient();
            downloadClient.Timeout = TimeSpan.FromMinutes(5);

            var cabBytes = await downloadClient.GetByteArrayAsync(cabUrl, cancellationToken);
            await File.WriteAllBytesAsync(cabFilePath, cabBytes, cancellationToken);

            // Extract CAB using expand.exe
            var expandExit = await _runner.RunAsync("expand.exe", $"-F:* \"{cabFilePath}\" \"{tempFolder}\"", tempFolder, cancellationToken);
            if (expandExit != 0)
            {
                throw new InvalidOperationException($"expand.exe falhou ao extrair o CAB com código {expandExit}");
            }

            var extractedFile = Path.Combine(tempFolder, "fil720cc132fbb53f3bed2e525eb77bdbc1");
            if (!File.Exists(extractedFile))
            {
                throw new FileNotFoundException("Arquivo oscdimg não encontrado no pacote extraído.");
            }

            File.Copy(extractedFile, destinationPath, overwrite: true);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, recursive: true);
            }
            catch { }
        }
    }

    public async Task<bool> BuildIsoAsync(
        string sourceDirectory,
        string outputIsoPath,
        string volumeLabel = "KISO11",
        CancellationToken cancellationToken = default)
    {
        var oscdimgExe = await EnsureOscdimgPathAsync(cancellationToken);

        var etfsbootPath = Path.Combine(sourceDirectory, "boot", "etfsboot.com");
        var efisysPath = Path.Combine(sourceDirectory, "efi", "microsoft", "boot", "efisys.bin");

        var bootData = $"2#p0,e,b\"{etfsbootPath}\"#pEF,e,b\"{efisysPath}\"";
        var arguments = $"-bootdata:{bootData} -m -o -h -u2 -udfver102 -l{volumeLabel} \"{sourceDirectory}\" \"{outputIsoPath}\"";

        var exitCode = await _runner.RunAsync(oscdimgExe, arguments, sourceDirectory, cancellationToken);
        return exitCode == 0 && File.Exists(outputIsoPath);
    }
}
