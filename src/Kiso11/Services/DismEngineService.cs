using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Kiso11.Models;

namespace Kiso11.Services;

public class DismEngineService
{
    private readonly ProcessRunner _runner;

    public DismEngineService(ProcessRunner runner)
    {
        _runner = runner;
    }

    public async Task<List<string>> GetWimEditionsAsync(string wimPath, CancellationToken cancellationToken = default)
    {
        var editions = new List<string>();
        if (!File.Exists(wimPath)) return editions;

        var output = await _runner.RunAndCaptureOutputAsync("dism.exe", $"/Get-WimInfo /WimFile:\"{wimPath}\" /English", null, cancellationToken);
        var matches = Regex.Matches(output, @"Name\s*:\s*(.+)");
        foreach (Match m in matches)
        {
            var name = m.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(name))
            {
                editions.Add(name);
            }
        }

        return editions;
    }

    public async Task MountWimAsync(string wimPath, int index, string mountDir, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(mountDir);
        var args = $"/Mount-Image /ImageFile:\"{wimPath}\" /Index:{index} /MountDir:\"{mountDir}\"";
        var exit = await _runner.RunAsync("dism.exe", args, null, cancellationToken);
        if (exit != 0)
        {
            throw new InvalidOperationException($"Falha ao montar WIM no diretório {mountDir} (código {exit}).");
        }
    }

    public async Task UnmountWimAsync(string mountDir, bool commit, CancellationToken cancellationToken = default)
    {
        var action = commit ? "/Commit" : "/Discard";
        var args = $"/Unmount-Image /MountDir:\"{mountDir}\" {action}";
        var exit = await _runner.RunAsync("dism.exe", args, null, cancellationToken);
        if (exit != 0)
        {
            throw new InvalidOperationException($"Falha ao desmontar WIM em {mountDir} (código {exit}).");
        }
    }

    public async Task CleanupMountDirAsync(string mountDir)
    {
        try
        {
            await _runner.RunAsync("dism.exe", $"/Unmount-Image /MountDir:\"{mountDir}\" /Discard", null, CancellationToken.None);
        }
        catch { }
        try
        {
            await _runner.RunAsync("dism.exe", "/Cleanup-Wim", null, CancellationToken.None);
        }
        catch { }
    }

    public async Task RemoveBloatwarePackagesAsync(string mountDir, Action<int, string> onProgress, CancellationToken cancellationToken = default)
    {
        string[] bloatPatterns =
        [
            "Microsoft.Microsoft3DViewer*",
            "Microsoft.WindowsAlarms*",
            "Microsoft.BingNews*",
            "Microsoft.BingSearch*",
            "Clipchamp.Clipchamp*",
            "Microsoft.549981C3F5F10*",
            "MicrosoftWindows.CrossDevice*",
            "Microsoft.Windows.DevHome*",
            "MicrosoftCorporationII.MicrosoftFamily*",
            "Microsoft.WindowsFeedbackHub*",
            "Microsoft.GetHelp*",
            "Microsoft.Getstarted*",
            "Microsoft.WindowsCommunicationsapps*",
            "Microsoft.WindowsMaps*",
            "Microsoft.MixedReality.Portal*",
            "Microsoft.ZuneMusic*",
            "Microsoft.MicrosoftOfficeHub*",
            "Microsoft.Office.OneNote*",
            "Microsoft.OutlookForWindows*",
            "Microsoft.MSPaint*",
            "Microsoft.People*",
            "Microsoft.YourPhone*",
            "Microsoft.PowerAutomateDesktop*",
            "MicrosoftCorporationII.QuickAssist*",
            "Microsoft.SkypeApp*",
            "Microsoft.MicrosoftStickyNotes*",
            "Microsoft.MicrosoftSolitaireCollection*",
            "MicrosoftTeams*",
            "MSTeams*",
            "Microsoft.Windows.Teams*",
            "Microsoft.Todos*",
            "Microsoft.ZuneVideo*",
            "Microsoft.Wallet*",
            "Microsoft.GamingApp*",
            "Microsoft.XboxApp*",
            "Microsoft.XboxGameOverlay*",
            "Microsoft.XboxGamingOverlay*",
            "Microsoft.XboxSpeechToTextOverlay*",
            "Microsoft.Xbox.TCUI*"
        ];

        var installedOutput = await _runner.RunAndCaptureOutputAsync("dism.exe", $"/Image:\"{mountDir}\" /Get-ProvisionedAppxPackages", null, cancellationToken);
        var pkgMatches = Regex.Matches(installedOutput, @"PackageName\s*:\s*(.+)");
        var installedPackages = pkgMatches.Select(m => m.Groups[1].Value.Trim()).ToList();

        var toRemove = new List<string>();
        foreach (var pattern in bloatPatterns)
        {
            var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            toRemove.AddRange(installedPackages.Where(p => Regex.IsMatch(p, regex, RegexOptions.IgnoreCase)));
        }

        toRemove = toRemove.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        int total = toRemove.Count;
        int current = 0;

        foreach (var pkg in toRemove)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current++;
            onProgress((int)(current * 100.0 / Math.Max(1, total)), $"Removendo bloatware ({current}/{total}): {pkg}");
            await _runner.RunAsync("dism.exe", $"/Image:\"{mountDir}\" /Remove-ProvisionedAppxPackage /PackageName:\"{pkg}\"", null, cancellationToken);
        }

        // Capabilities (Features on Demand)
        string[] capabilities =
        [
            "Browser.InternetExplorer*",
            "App.StepsRecorder*",
            "Microsoft.Windows.WordPad*",
            "MathRecognizer*",
            "Microsoft.Windows.PowerShell.ISE*",
            "Media.WindowsMediaPlayer*"
        ];

        var capsOutput = await _runner.RunAndCaptureOutputAsync("dism.exe", $"/Image:\"{mountDir}\" /Get-Capabilities", null, cancellationToken);
        var capMatches = Regex.Matches(capsOutput, @"Capability Identity\s*:\s*(.+)");
        var installedCaps = capMatches.Select(m => m.Groups[1].Value.Trim()).ToList();

        foreach (var capPattern in capabilities)
        {
            var regex = "^" + Regex.Escape(capPattern).Replace("\\*", ".*") + "$";
            var matchedCaps = installedCaps.Where(c => Regex.IsMatch(c, regex, RegexOptions.IgnoreCase)).ToList();
            foreach (var cap in matchedCaps)
            {
                await _runner.RunAsync("dism.exe", $"/Image:\"{mountDir}\" /Remove-Capability /CapabilityName:\"{cap}\"", null, cancellationToken);
            }
        }
    }

    public async Task RemoveAiComponentsAsync(string mountDir, Action<int, string> onProgress, CancellationToken cancellationToken = default)
    {
        string[] aiPatterns =
        [
            "Microsoft.Windows.Copilot*",
            "Microsoft.Copilot*",
            "MicrosoftWindows.Client.AIX*",
            "MicrosoftWindows.Client.CoPilot*",
            "MicrosoftWindows.Client.CoreAI*",
            "Microsoft.Windows.Ai.Copilot.Provider*",
            "Microsoft.Edge.GameAssist*",
            "Microsoft.Office.ActionsServer*",
            "Microsoft.WritingAssistant*"
        ];

        var installedOutput = await _runner.RunAndCaptureOutputAsync("dism.exe", $"/Image:\"{mountDir}\" /Get-ProvisionedAppxPackages", null, cancellationToken);
        var pkgMatches = Regex.Matches(installedOutput, @"PackageName\s*:\s*(.+)");
        var installedPackages = pkgMatches.Select(m => m.Groups[1].Value.Trim()).ToList();

        foreach (var pattern in aiPatterns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            var matched = installedPackages.Where(p => Regex.IsMatch(p, regex, RegexOptions.IgnoreCase)).ToList();

            foreach (var pkg in matched)
            {
                onProgress(50, $"Removendo componente de IA: {pkg}");
                await _runner.RunAsync("dism.exe", $"/Image:\"{mountDir}\" /Remove-ProvisionedAppxPackage /PackageName:\"{pkg}\"", null, cancellationToken);
            }
        }

        // Disable Recall feature if present
        await _runner.RunAsync("dism.exe", $"/Image:\"{mountDir}\" /Disable-Feature /FeatureName:Recall /Remove", null, cancellationToken);
    }

    public async Task RemoveOneDriveFilesAsync(string mountDir, CancellationToken cancellationToken = default)
    {
        var pathsToRemove = new[]
        {
            Path.Combine(mountDir, @"Windows\System32\OneDriveSetup.exe"),
            Path.Combine(mountDir, @"Windows\SysWOW64\OneDriveSetup.exe"),
            Path.Combine(mountDir, @"Users\Default\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\OneDrive.lnk")
        };

        foreach (var path in pathsToRemove)
        {
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch { }
            }
        }

        await Task.CompletedTask;
    }

    public async Task RemoveEdgeAsync(string mountDir, CancellationToken cancellationToken = default)
    {
        await _runner.RunAsync("dism.exe", $"/Image:\"{mountDir}\" /Remove-Edge", null, cancellationToken);

        string[] edgePatterns =
        [
            "Microsoft.MicrosoftEdge.Stable*",
            "Microsoft.MicrosoftEdgeDevToolsClient*",
            "Microsoft.Win32WebViewHost*",
            "MicrosoftWindows.Client.WebExperience*"
        ];

        var installedOutput = await _runner.RunAndCaptureOutputAsync("dism.exe", $"/Image:\"{mountDir}\" /Get-ProvisionedAppxPackages", null, cancellationToken);
        var pkgMatches = Regex.Matches(installedOutput, @"PackageName\s*:\s*(.+)");
        var installedPackages = pkgMatches.Select(m => m.Groups[1].Value.Trim()).ToList();

        foreach (var pattern in edgePatterns)
        {
            var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            var matched = installedPackages.Where(p => Regex.IsMatch(p, regex, RegexOptions.IgnoreCase));
            foreach (var pkg in matched)
            {
                await _runner.RunAsync("dism.exe", $"/Image:\"{mountDir}\" /Remove-ProvisionedAppxPackage /PackageName:\"{pkg}\"", null, cancellationToken);
            }
        }
    }

    public async Task ApplyOfflineRegistryTweaksAsync(string mountDir, DebloatOptions options, CancellationToken cancellationToken = default)
    {
        var softwareHive = Path.Combine(mountDir, @"Windows\System32\config\SOFTWARE");
        var systemHive = Path.Combine(mountDir, @"Windows\System32\config\SYSTEM");
        var ntuserHive = Path.Combine(mountDir, @"Users\Default\ntuser.dat");

        try
        {
            await _runner.RunAsync("reg.exe", $"load HKLM\\kSOFTWARE \"{softwareHive}\"", null, cancellationToken);
            await _runner.RunAsync("reg.exe", $"load HKLM\\kSYSTEM \"{systemHive}\"", null, cancellationToken);
            await _runner.RunAsync("reg.exe", $"load HKLM\\kNTUSER \"{ntuserHive}\"", null, cancellationToken);

            // 1. BitLocker 24H2 - Prevent automatic device encryption
            if (options.DisableBitlockerEncryption)
            {
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kSYSTEM\\ControlSet001\\Control\\BitLocker\" /v \"PreventDeviceEncryption\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
            }

            // 2. Telemetry & Privacy
            if (options.DisableTelemetryAndAds)
            {
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kSOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection\" /v \"AllowTelemetry\" /t REG_DWORD /d \"0\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kSYSTEM\\ControlSet001\\Services\\dmwappushservice\" /v \"Start\" /t REG_DWORD /d \"4\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kNTUSER\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo\" /v \"Enabled\" /t REG_DWORD /d \"0\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kSOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent\" /v \"DisableWindowsConsumerFeatures\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kSOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent\" /v \"DisableCloudOptimizedContent\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kNTUSER\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager\" /v \"PreInstalledAppsEnabled\" /t REG_DWORD /d \"0\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kNTUSER\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager\" /v \"SilentInstalledAppsEnabled\" /t REG_DWORD /d \"0\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kNTUSER\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager\" /v \"SubscribedContentEnabled\" /t REG_DWORD /d \"0\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kNTUSER\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v \"Start_IrisRecommendations\" /t REG_DWORD /d \"0\" /f", null, cancellationToken);
            }

            // 3. OOBE Bypass - Local account
            if (options.BypassMicrosoftAccount)
            {
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kSOFTWARE\\Microsoft\\Windows\\CurrentVersion\\OOBE\" /v \"BypassNRO\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kSOFTWARE\\Policies\\Microsoft\\Windows\\OOBE\" /v \"DisablePrivacyExperience\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
            }

            // 4. AI & Copilot policies
            if (options.RemoveAiAndCopilot)
            {
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kSOFTWARE\\Policies\\Microsoft\\Windows\\WindowsCopilot\" /v \"TurnOffWindowsCopilot\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kSOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI\" /v \"DisableAIDataAnalysis\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kSOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI\" /v \"AllowRecallEnablement\" /t REG_DWORD /d \"0\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kSOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI\" /v \"TurnOffSavingSnapshots\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kSOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI\" /v \"DisableClickToDo\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kSOFTWARE\\Policies\\WindowsNotepad\" /v \"DisableAIFeatures\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kNTUSER\\Software\\Microsoft\\Windows\\CurrentVersion\\WindowsCopilot\" /v \"AllowCopilotRuntime\" /t REG_DWORD /d \"0\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kNTUSER\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v \"ShowCopilotButton\" /t REG_DWORD /d \"0\" /f", null, cancellationToken);
            }

            // 5. User Folders in "This PC"
            if (options.RestoreUserFolders)
            {
                string[] folderGuids =
                [
                    "{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}", // Desktop
                    "{d3162b92-9365-467a-956b-92703aca08af}", // Documents
                    "{088e3905-0323-4b02-9826-5d99428e115f}", // Downloads
                    "{3dfdf296-dbec-4fb4-81d1-6a3438bcf4de}", // Music
                    "{24ad3ad4-a569-4530-98e1-ab02f9417aa8}", // Pictures
                    "{f86fa3ab-70d2-4fc7-9c99-fcbf05467f3a}"  // Videos
                ];

                foreach (var guid in folderGuids)
                {
                    await _runner.RunAsync("reg.exe", $"add \"HKLM\\kSOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\MyComputer\\NameSpace\\{guid}\" /v \"HideIfEnabled\" /t REG_DWORD /d \"0\" /f", null, cancellationToken);
                }
            }

            // 6. GameBar popup fix
            await _runner.RunAsync("reg.exe", "add \"HKLM\\kNTUSER\\Software\\Microsoft\\GameBar\" /v \"AutoGameModeEnabled\" /t REG_DWORD /d \"0\" /f", null, cancellationToken);

            // 7. Hardware LabConfig bypasses in install.wim
            if (options.BypassTpmAndHardware)
            {
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kSYSTEM\\Setup\\LabConfig\" /v \"BypassTPMCheck\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kSYSTEM\\Setup\\LabConfig\" /v \"BypassSecureBootCheck\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kSYSTEM\\Setup\\LabConfig\" /v \"BypassRAMCheck\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kSYSTEM\\Setup\\LabConfig\" /v \"BypassCPUCheck\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kSYSTEM\\Setup\\LabConfig\" /v \"BypassStorageCheck\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kSYSTEM\\Setup\\LabConfig\" /v \"BypassDiskCheck\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
                await _runner.RunAsync("reg.exe", "add \"HKLM\\kSYSTEM\\Setup\\MoSetup\" /v \"AllowUpgradesWithUnsupportedTPMOrCPU\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
            }
        }
        finally
        {
            await _runner.RunAsync("reg.exe", "unload HKLM\\kSOFTWARE", null, CancellationToken.None);
            await _runner.RunAsync("reg.exe", "unload HKLM\\kSYSTEM", null, CancellationToken.None);
            await _runner.RunAsync("reg.exe", "unload HKLM\\kNTUSER", null, CancellationToken.None);
        }
    }

    public async Task ApplyBootWimBypassesAsync(string bootWimPath, string bootMountDir, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(bootWimPath)) return;

        Directory.CreateDirectory(bootMountDir);
        try
        {
            await _runner.RunAsync("dism.exe", $"/Mount-Image /ImageFile:\"{bootWimPath}\" /Index:2 /MountDir:\"{bootMountDir}\"", null, cancellationToken);

            var systemHive = Path.Combine(bootMountDir, @"Windows\System32\config\SYSTEM");
            if (File.Exists(systemHive))
            {
                try
                {
                    await _runner.RunAsync("reg.exe", $"load HKLM\\xSYSTEM \"{systemHive}\"", null, cancellationToken);
                    await _runner.RunAsync("reg.exe", "add \"HKLM\\xSYSTEM\\Setup\\LabConfig\" /v \"BypassTPMCheck\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
                    await _runner.RunAsync("reg.exe", "add \"HKLM\\xSYSTEM\\Setup\\LabConfig\" /v \"BypassSecureBootCheck\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
                    await _runner.RunAsync("reg.exe", "add \"HKLM\\xSYSTEM\\Setup\\LabConfig\" /v \"BypassRAMCheck\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
                    await _runner.RunAsync("reg.exe", "add \"HKLM\\xSYSTEM\\Setup\\LabConfig\" /v \"BypassCPUCheck\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
                    await _runner.RunAsync("reg.exe", "add \"HKLM\\xSYSTEM\\Setup\\LabConfig\" /v \"BypassStorageCheck\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
                    await _runner.RunAsync("reg.exe", "add \"HKLM\\xSYSTEM\\Setup\\LabConfig\" /v \"BypassDiskCheck\" /t REG_DWORD /d \"1\" /f", null, cancellationToken);
                }
                finally
                {
                    await _runner.RunAsync("reg.exe", "unload HKLM\\xSYSTEM", null, CancellationToken.None);
                }
            }

            await _runner.RunAsync("dism.exe", $"/Unmount-Image /MountDir:\"{bootMountDir}\" /Commit", null, cancellationToken);
        }
        catch
        {
            try { await _runner.RunAsync("dism.exe", $"/Unmount-Image /MountDir:\"{bootMountDir}\" /Discard", null, CancellationToken.None); } catch { }
        }
    }

    public async Task IntegrateDriversAsync(string mountDir, string driversDir, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(driversDir)) return;
        await _runner.RunAsync("dism.exe", $"/Image:\"{mountDir}\" /Add-Driver /Driver:\"{driversDir}\" /Recurse /ForceUnsigned", null, cancellationToken);
    }

    public async Task OptimizeAndExportWimAsync(string sourceWim, bool compressEsd, CancellationToken cancellationToken = default)
    {
        var tempWim = Path.Combine(Path.GetDirectoryName(sourceWim)!, "install_optimized.wim");
        var compressArg = compressEsd ? "/Compress:recovery" : "/Compress:max";

        var exit = await _runner.RunAsync("dism.exe", $"/Export-Image /SourceImageFile:\"{sourceWim}\" /SourceIndex:1 /DestinationImageFile:\"{tempWim}\" {compressArg} /CheckIntegrity", null, cancellationToken);
        if (exit == 0 && File.Exists(tempWim))
        {
            File.Delete(sourceWim);
            File.Move(tempWim, sourceWim);
        }
    }
}
