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
    private static readonly string[] BloatwarePatterns =
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

    private static readonly string[] BloatCapabilityPatterns =
    [
        "Browser.InternetExplorer*",
        "App.StepsRecorder*",
        "Microsoft.Windows.WordPad*",
        "MathRecognizer*",
        "Microsoft.Windows.PowerShell.ISE*",
        "Media.WindowsMediaPlayer*"
    ];

    private static readonly string[] AiPatterns =
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

    private static readonly string[] EdgePatterns =
    [
        "Microsoft.MicrosoftEdge.Stable*",
        "Microsoft.MicrosoftEdgeDevToolsClient*",
        "Microsoft.Win32WebViewHost*",
        "MicrosoftWindows.Client.WebExperience*"
    ];

    private static readonly string[] UserFolderGuids =
    [
        "{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}", // Desktop
        "{d3162b92-9365-467a-956b-92703aca08af}", // Documents
        "{088e3905-0323-4b02-9826-5d99428e115f}", // Downloads
        "{3dfdf296-dbec-4fb4-81d1-6a3438bcf4de}", // Music
        "{24ad3ad4-a569-4530-98e1-ab02f9417aa8}", // Pictures
        "{f86fa3ab-70d2-4fc7-9c99-fcbf05467f3a}"  // Videos
    ];

    private static readonly string[] LabConfigValues =
    [
        "BypassTPMCheck",
        "BypassSecureBootCheck",
        "BypassRAMCheck",
        "BypassCPUCheck",
        "BypassStorageCheck",
        "BypassDiskCheck"
    ];

    private readonly ProcessRunner _runner;

    public DismEngineService(ProcessRunner runner)
    {
        _runner = runner;
    }

    /// Reads the editions from `dism /Get-WimInfo` output. The output is requested with /English so the labels match.
    public static List<WimEdition> ParseEditions(string output)
    {
        var editions = new List<WimEdition>();
        int? pendingIndex = null;

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();

            var indexMatch = Regex.Match(line, @"^Index\s*:\s*(\d+)$");
            if (indexMatch.Success)
            {
                pendingIndex = int.Parse(indexMatch.Groups[1].Value);
                continue;
            }

            var nameMatch = Regex.Match(line, @"^Name\s*:\s*(.+)$");
            if (nameMatch.Success && pendingIndex is int index)
            {
                editions.Add(new WimEdition(index, nameMatch.Groups[1].Value.Trim()));
                pendingIndex = null;
            }
        }

        return editions;
    }

    public async Task<List<WimEdition>> GetWimEditionsAsync(string wimPath, CancellationToken cancellationToken = default)
    {
        var (exitCode, output) = await _runner.RunCaptureAsync(
            "dism.exe",
            $"/Get-WimInfo /WimFile:\"{wimPath}\" /English",
            null,
            cancellationToken,
            log: false);

        if (exitCode != 0)
        {
            throw new InvalidOperationException(LocalizationService.Instance.ErrReadEditions);
        }

        return ParseEditions(output);
    }

    public async Task MountWimAsync(string wimPath, int index, string mountDir, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(mountDir);
        var args = $"/Mount-Image /ImageFile:\"{wimPath}\" /Index:{index} /MountDir:\"{mountDir}\"";
        var exit = await _runner.RunAsync("dism.exe", args, null, cancellationToken);
        if (exit != 0)
        {
            throw new InvalidOperationException(string.Format(LocalizationService.Instance.ErrMountWim, mountDir, exit));
        }
    }

    public async Task UnmountWimAsync(string mountDir, bool commit, CancellationToken cancellationToken = default)
    {
        var action = commit ? "/Commit" : "/Discard";
        var args = $"/Unmount-Image /MountDir:\"{mountDir}\" {action}";
        var exit = await _runner.RunAsync("dism.exe", args, null, cancellationToken);
        if (exit != 0)
        {
            throw new InvalidOperationException(string.Format(LocalizationService.Instance.ErrUnmountWim, mountDir, exit));
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
        var loc = LocalizationService.Instance;
        var installedPackages = await GetProvisionedPackagesAsync(mountDir, cancellationToken);

        var toRemove = MatchPatterns(installedPackages, BloatwarePatterns).ToList();
        int total = toRemove.Count;
        int current = 0;

        foreach (var pkg in toRemove)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current++;
            onProgress((int)(current * 100.0 / Math.Max(1, total)), string.Format(loc.MsgRemovingApps, current, total));
            await RemovePackageAsync(mountDir, pkg, cancellationToken);
        }

        // Optional Windows features (Features on Demand)
        var (capExit, capsOutput) = await _runner.RunCaptureAsync(
            "dism.exe",
            $"/Image:\"{mountDir}\" /Get-Capabilities /English",
            null,
            cancellationToken,
            log: false);

        if (capExit != 0) return;

        var installedCaps = Regex.Matches(capsOutput, @"Capability Identity\s*:\s*(.+)")
            .Select(m => m.Groups[1].Value.Trim())
            .ToList();

        foreach (var cap in MatchPatterns(installedCaps, BloatCapabilityPatterns))
        {
            await _runner.RunAsync("dism.exe", $"/Image:\"{mountDir}\" /Remove-Capability /CapabilityName:\"{cap}\"", null, cancellationToken);
        }
    }

    public async Task RemoveAiComponentsAsync(string mountDir, Action<int, string> onProgress, CancellationToken cancellationToken = default)
    {
        var loc = LocalizationService.Instance;
        var installedPackages = await GetProvisionedPackagesAsync(mountDir, cancellationToken);

        foreach (var pkg in MatchPatterns(installedPackages, AiPatterns))
        {
            cancellationToken.ThrowIfCancellationRequested();
            onProgress(50, string.Format(loc.MsgRemovingAi, pkg));
            await RemovePackageAsync(mountDir, pkg, cancellationToken);
        }

        // The Recall feature is missing on some builds, so a failure here is expected and ignored.
        await _runner.RunCaptureAsync("dism.exe", $"/Image:\"{mountDir}\" /Disable-Feature /FeatureName:Recall /Remove", null, cancellationToken, log: false);
    }

    public Task RemoveOneDriveFilesAsync(string mountDir, CancellationToken cancellationToken = default)
    {
        var pathsToRemove = new[]
        {
            Path.Combine(mountDir, @"Windows\System32\OneDriveSetup.exe"),
            Path.Combine(mountDir, @"Windows\SysWOW64\OneDriveSetup.exe"),
            Path.Combine(mountDir, @"Users\Default\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\OneDrive.lnk")
        };

        foreach (var path in pathsToRemove)
        {
            if (!File.Exists(path)) continue;

            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                _runner.Report(string.Format(LocalizationService.Instance.LogFileDeleteFailed, path, ex.Message));
            }
        }

        return Task.CompletedTask;
    }

    public async Task RemoveEdgeAsync(string mountDir, CancellationToken cancellationToken = default)
    {
        var exit = await _runner.RunAsync("dism.exe", $"/Image:\"{mountDir}\" /Remove-Edge", null, cancellationToken);
        if (exit != 0)
        {
            _runner.Report(string.Format(LocalizationService.Instance.LogEdgeRemoveFailed, exit));
        }

        var installedPackages = await GetProvisionedPackagesAsync(mountDir, cancellationToken);
        foreach (var pkg in MatchPatterns(installedPackages, EdgePatterns))
        {
            await RemovePackageAsync(mountDir, pkg, cancellationToken);
        }
    }

    public async Task ApplyOfflineRegistryTweaksAsync(string mountDir, DebloatOptions options, CancellationToken cancellationToken = default)
    {
        var loc = LocalizationService.Instance;
        var loadedHives = new List<string>();

        try
        {
            await LoadHiveAsync("kSOFTWARE", Path.Combine(mountDir, @"Windows\System32\config\SOFTWARE"), loadedHives, cancellationToken);
            await LoadHiveAsync("kSYSTEM", Path.Combine(mountDir, @"Windows\System32\config\SYSTEM"), loadedHives, cancellationToken);
            await LoadHiveAsync("kNTUSER", Path.Combine(mountDir, @"Users\Default\ntuser.dat"), loadedHives, cancellationToken);

            int failures = 0;
            foreach (var (key, name, value) in BuildRegistryTweaks(options))
            {
                var (exit, _) = await _runner.RunCaptureAsync(
                    "reg.exe",
                    $"add \"HKLM\\{key}\" /v \"{name}\" /t REG_DWORD /d \"{value}\" /f",
                    null,
                    cancellationToken,
                    log: false);

                if (exit != 0)
                {
                    failures++;
                    _runner.Report(string.Format(loc.LogRegistryValueFailed, name, key));
                }
            }

            if (failures > 0)
            {
                _runner.Report(string.Format(loc.LogRegistryFailures, failures));
            }
        }
        finally
        {
            for (int i = loadedHives.Count - 1; i >= 0; i--)
            {
                var (exit, _) = await _runner.RunCaptureAsync("reg.exe", $"unload HKLM\\{loadedHives[i]}", null, CancellationToken.None, log: false);
                if (exit != 0)
                {
                    _runner.Report(string.Format(loc.LogHiveUnloadFailed, loadedHives[i]));
                }
            }
        }
    }

    /// Applies the LabConfig bypass to boot.wim so Windows Setup skips hardware checks. Failures only add a warning, because the rest of the image is still usable.
    public async Task ApplyBootWimBypassesAsync(string bootWimPath, string bootMountDir, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(bootWimPath)) return;

        Directory.CreateDirectory(bootMountDir);
        var loc = LocalizationService.Instance;
        var mounted = false;

        try
        {
            var (mountExit, _) = await _runner.RunCaptureAsync(
                "dism.exe",
                $"/Mount-Image /ImageFile:\"{bootWimPath}\" /Index:2 /MountDir:\"{bootMountDir}\"",
                null,
                cancellationToken);

            if (mountExit != 0)
            {
                _runner.Report(string.Format(loc.LogBootWimSkipped, mountExit));
                return;
            }

            mounted = true;
            var systemHive = Path.Combine(bootMountDir, @"Windows\System32\config\SYSTEM");
            var loaded = new List<string>();

            try
            {
                await LoadHiveAsync("xSYSTEM", systemHive, loaded, cancellationToken);
                foreach (var value in LabConfigValues)
                {
                    await _runner.RunCaptureAsync("reg.exe", $"add \"HKLM\\xSYSTEM\\Setup\\LabConfig\" /v \"{value}\" /t REG_DWORD /d \"1\" /f", null, cancellationToken, log: false);
                }
            }
            finally
            {
                if (loaded.Count > 0)
                {
                    await _runner.RunCaptureAsync("reg.exe", "unload HKLM\\xSYSTEM", null, CancellationToken.None, log: false);
                }
            }

            await UnmountWimAsync(bootMountDir, commit: true, cancellationToken);
            mounted = false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _runner.Report(string.Format(loc.LogBootWimSkipped, ex.Message));
        }
        finally
        {
            if (mounted)
            {
                await _runner.RunCaptureAsync("dism.exe", $"/Unmount-Image /MountDir:\"{bootMountDir}\" /Discard", null, CancellationToken.None, log: false);
            }
        }
    }

    public async Task IntegrateDriversAsync(string mountDir, string driversDir, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(driversDir)) return;

        var exit = await _runner.RunAsync("dism.exe", $"/Image:\"{mountDir}\" /Add-Driver /Driver:\"{driversDir}\" /Recurse /ForceUnsigned", null, cancellationToken);
        if (exit != 0)
        {
            _runner.Report(string.Format(LocalizationService.Instance.LogDriversFailed, exit));
        }
    }

    /// Exports only the chosen edition, so the output ISO contains one image at index 1.
    public async Task OptimizeAndExportWimAsync(string sourceWim, int sourceIndex, bool compressEsd, CancellationToken cancellationToken = default)
    {
        var tempWim = Path.Combine(Path.GetDirectoryName(sourceWim)!, "install_optimized.wim");
        var compressArg = compressEsd ? "/Compress:recovery" : "/Compress:max";

        var exit = await _runner.RunAsync(
            "dism.exe",
            $"/Export-Image /SourceImageFile:\"{sourceWim}\" /SourceIndex:{sourceIndex} /DestinationImageFile:\"{tempWim}\" {compressArg} /CheckIntegrity",
            null,
            cancellationToken);

        if (exit != 0 || !File.Exists(tempWim))
        {
            throw new InvalidOperationException(string.Format(LocalizationService.Instance.ErrExportWim, exit));
        }

        File.Delete(sourceWim);
        File.Move(tempWim, sourceWim);
    }

    private async Task<List<string>> GetProvisionedPackagesAsync(string mountDir, CancellationToken cancellationToken)
    {
        var (_, output) = await _runner.RunCaptureAsync(
            "dism.exe",
            $"/Image:\"{mountDir}\" /Get-ProvisionedAppxPackages /English",
            null,
            cancellationToken,
            log: false);

        return Regex.Matches(output, @"PackageName\s*:\s*(.+)")
            .Select(m => m.Groups[1].Value.Trim())
            .ToList();
    }

    private Task RemovePackageAsync(string mountDir, string packageName, CancellationToken cancellationToken)
        => _runner.RunAsync("dism.exe", $"/Image:\"{mountDir}\" /Remove-ProvisionedAppxPackage /PackageName:\"{packageName}\"", null, cancellationToken);

    private static IEnumerable<string> MatchPatterns(IEnumerable<string> names, IEnumerable<string> wildcardPatterns)
    {
        var matches = new List<string>();
        foreach (var pattern in wildcardPatterns)
        {
            var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            matches.AddRange(names.Where(n => Regex.IsMatch(n, regex, RegexOptions.IgnoreCase)));
        }

        return matches.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task LoadHiveAsync(string hiveName, string hiveFile, List<string> loadedHives, CancellationToken cancellationToken)
    {
        var (exit, _) = await _runner.RunCaptureAsync("reg.exe", $"load HKLM\\{hiveName} \"{hiveFile}\"", null, cancellationToken, log: false);
        if (exit != 0)
        {
            throw new InvalidOperationException(string.Format(LocalizationService.Instance.ErrLoadHive, hiveFile));
        }

        loadedHives.Add(hiveName);
    }

    private static IEnumerable<(string Key, string Name, int Value)> BuildRegistryTweaks(DebloatOptions options)
    {
        // 1. BitLocker 24H2: stop automatic device encryption.
        if (options.DisableBitlockerEncryption)
        {
            yield return ("kSYSTEM\\ControlSet001\\Control\\BitLocker", "PreventDeviceEncryption", 1);
        }

        // 2. Telemetry and ads.
        if (options.DisableTelemetryAndAds)
        {
            yield return ("kSOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection", "AllowTelemetry", 0);
            yield return ("kSYSTEM\\ControlSet001\\Services\\dmwappushservice", "Start", 4);
            yield return ("kNTUSER\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo", "Enabled", 0);
            yield return ("kSOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent", "DisableWindowsConsumerFeatures", 1);
            yield return ("kSOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent", "DisableCloudOptimizedContent", 1);
            yield return ("kNTUSER\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager", "PreInstalledAppsEnabled", 0);
            yield return ("kNTUSER\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager", "SilentInstalledAppsEnabled", 0);
            yield return ("kNTUSER\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager", "SubscribedContentEnabled", 0);
            yield return ("kNTUSER\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "Start_IrisRecommendations", 0);
        }

        // 3. OOBE: allow a local account without internet.
        if (options.BypassMicrosoftAccount)
        {
            yield return ("kSOFTWARE\\Microsoft\\Windows\\CurrentVersion\\OOBE", "BypassNRO", 1);
            yield return ("kSOFTWARE\\Policies\\Microsoft\\Windows\\OOBE", "DisablePrivacyExperience", 1);
        }

        // 4. AI and Copilot policies.
        if (options.RemoveAiAndCopilot)
        {
            yield return ("kSOFTWARE\\Policies\\Microsoft\\Windows\\WindowsCopilot", "TurnOffWindowsCopilot", 1);
            yield return ("kSOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI", "DisableAIDataAnalysis", 1);
            yield return ("kSOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI", "AllowRecallEnablement", 0);
            yield return ("kSOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI", "TurnOffSavingSnapshots", 1);
            yield return ("kSOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI", "DisableClickToDo", 1);
            yield return ("kSOFTWARE\\Policies\\WindowsNotepad", "DisableAIFeatures", 1);
            yield return ("kNTUSER\\Software\\Microsoft\\Windows\\CurrentVersion\\WindowsCopilot", "AllowCopilotRuntime", 0);
            yield return ("kNTUSER\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "ShowCopilotButton", 0);
        }

        // 5. User folders visible in "This PC".
        if (options.RestoreUserFolders)
        {
            foreach (var guid in UserFolderGuids)
            {
                yield return ($"kSOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\MyComputer\\NameSpace\\{guid}", "HideIfEnabled", 0);
            }
        }

        // 6. Game Bar pop-up fix (always applied).
        yield return ("kNTUSER\\Software\\Microsoft\\GameBar", "AutoGameModeEnabled", 0);

        // 7. Hardware requirement bypasses in install.wim.
        if (options.BypassTpmAndHardware)
        {
            foreach (var value in LabConfigValues)
            {
                yield return ("kSYSTEM\\Setup\\LabConfig", value, 1);
            }

            yield return ("kSYSTEM\\Setup\\MoSetup", "AllowUpgradesWithUnsupportedTPMOrCPU", 1);
        }
    }
}
