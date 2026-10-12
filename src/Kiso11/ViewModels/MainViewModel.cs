using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Kiso11.Models;
using Kiso11.Services;

namespace Kiso11.ViewModels;

public class MainViewModel : BaseViewModel
{
    private const long LogBufferLimit = 200_000;

    private readonly ProcessRunner _runner;
    private readonly IsoService _isoService;
    private readonly DismEngineService _dismService;
    private readonly UsbBootService _usbService;
    private readonly OscdimgService _oscdimgService;

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();
    private readonly StringBuilder _logBuffer = new();

    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _inspectCts;
    private Task? _runTask;

    private string _isoPath = string.Empty;
    private string _isoFileName = string.Empty;
    private string _isoSizeText = "-";
    private bool _isIsoValid;
    private bool _isInspecting;
    private string _inspectionError = string.Empty;
    private WimEdition? _selectedEdition;

    private OutputMode _outputMode = OutputMode.IsoFile;
    private string _outputIsoPath = string.Empty;
    private UsbDriveItem? _selectedUsbDrive;

    private int _currentStep = 1;
    private ProcessState _state = ProcessState.Idle;
    private int _progressPercentage;
    private string _statusMessage = string.Empty;
    private string _currentStageText = string.Empty;
    private string _elapsedTimeFormatted = "00:00";
    private string _estimatedRemainingFormatted = "--:--";
    private string _logText = string.Empty;

    public MainViewModel()
    {
        _runner = new ProcessRunner();
        _dismService = new DismEngineService(_runner);
        _isoService = new IsoService(_runner, _dismService);
        _usbService = new UsbBootService(_runner);
        _oscdimgService = new OscdimgService(_runner);

        _runner.LineReceived += OnLogLineReceived;

        UsbDrives = new ObservableCollection<UsbDriveItem>();
        UsbDrives.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasUsbDrives));
        DebloatOptions = new DebloatOptions();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;

        BrowseIsoCommand = new RelayCommand(BrowseIso);
        BrowseOutputIsoCommand = new RelayCommand(BrowseOutputIso);
        RefreshUsbDrivesCommand = new RelayCommand(RefreshUsbDrives);
        SelectPresetRecommendedCommand = new RelayCommand(SelectPresetRecommended);
        StartProcessCommand = new RelayCommand(() => _runTask = StartProcessAsync(), CanStartProcess);
        CancelProcessCommand = new RelayCommand(CancelProcess, () => IsProcessing);
        CopyLogCommand = new RelayCommand(CopyLog);
        ClearLogCommand = new RelayCommand(ClearLog);
        RestartAsAdminCommand = new RelayCommand(RestartAsAdmin);
        MinimizeToTrayCommand = new RelayCommand(() => RequestMinimizeToTray?.Invoke());

        NextStepCommand = new RelayCommand(NextStep, () => CanGoNext);
        PreviousStepCommand = new RelayCommand(PreviousStep, () => CanGoBack);
        StartNewDebloatCommand = new RelayCommand(StartNewDebloat);
        GoToStepCommand = new RelayCommand(p =>
        {
            if (p != null && int.TryParse(p.ToString(), out int s))
            {
                GoToStep(s);
            }
        });

        RefreshUsbDrives();

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        OutputIsoPath = Path.Combine(desktop, "Windows11_Kiso11_Debloated.iso");
        _isoFileName = Loc.NoIsoSelected;
        _statusMessage = Loc.ReadyToStart;
        _currentStageText = Loc.WaitingSelection;
    }

    #region Properties

    public LocalizationService Loc => LocalizationService.Instance;

    public RelayCommand MinimizeToTrayCommand { get; }
    public RelayCommand NextStepCommand { get; }
    public RelayCommand PreviousStepCommand { get; }
    public RelayCommand StartNewDebloatCommand { get; }
    public RelayCommand GoToStepCommand { get; }

    public int CurrentStep
    {
        get => _currentStep;
        set
        {
            if (SetProperty(ref _currentStep, value))
            {
                OnPropertyChanged(nameof(IsStep1));
                OnPropertyChanged(nameof(IsStep2));
                OnPropertyChanged(nameof(IsStep3));
                OnPropertyChanged(nameof(IsStep4));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(CanGoBack));
                NotifyFooterButtons();
                ((RelayCommand)NextStepCommand).RaiseCanExecuteChanged();
                ((RelayCommand)PreviousStepCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;

    public bool HasIsoSelected => !string.IsNullOrEmpty(IsoPath);

    public bool CanGoNext => CurrentStep switch
    {
        1 => IsIsoValid && !IsInspecting && SelectedEdition != null,
        2 => OutputMode == OutputMode.BootableUsb ? SelectedUsbDrive is { IsSupported: true } : !string.IsNullOrWhiteSpace(OutputIsoPath),
        3 => CanStartProcess(),
        _ => false
    };

    public bool CanGoBack => CurrentStep > 1 && CurrentStep < 4 && State != ProcessState.Running;

    public bool ShowNextButton => CurrentStep is 1 or 2 && !IsProcessing;
    public bool ShowStartButton => CurrentStep == 3 && !IsProcessing;
    public bool ShowCancelButton => IsProcessing;
    public bool ShowNewRunButton => CurrentStep == 4 && !IsProcessing;
    public bool HasUsbDrives => UsbDrives.Count > 0;
    public bool IsInspectionError => !IsInspecting && !string.IsNullOrEmpty(_inspectionError);

    public void NextStep()
    {
        if (CurrentStep < 3)
        {
            CurrentStep++;
        }
        else if (CurrentStep == 3)
        {
            StartProcessCommand.Execute(null);
        }
    }

    public void PreviousStep()
    {
        if (CurrentStep > 1 && State != ProcessState.Running)
        {
            CurrentStep--;
        }
    }

    public void GoToStep(int step)
    {
        if (State == ProcessState.Running) return;
        if (step == 2 && !IsIsoValid) return;
        if (step == 3 && (!IsIsoValid || (OutputMode == OutputMode.BootableUsb ? SelectedUsbDrive is not { IsSupported: true } : string.IsNullOrWhiteSpace(OutputIsoPath)))) return;
        if (step >= 1 && step <= 3)
        {
            CurrentStep = step;
        }
    }

    private void StartNewDebloat()
    {
        if (State == ProcessState.Running) return;
        State = ProcessState.Idle;
        ProgressPercentage = 0;
        StatusMessage = Loc.ReadyToStart;
        CurrentStageText = Loc.WaitingSelection;
        CurrentStep = 1;
    }

    public event Action? RequestMinimizeToTray;
    public event Action<string>? TrayStatusUpdated;
    public event Action? TrayCompleted;

    public bool IsAdministrator => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    public RelayCommand RestartAsAdminCommand { get; }

    public ObservableCollection<UsbDriveItem> UsbDrives { get; }
    public ObservableCollection<WimEdition> Editions { get; } = new();
    public DebloatOptions DebloatOptions { get; }

    public bool IsPtBrSelected
    {
        get => Loc.IsPtBr;
        set { if (value) SetLanguage(AppLanguage.PtBr); }
    }

    public bool IsEnSelected
    {
        get => Loc.IsEn;
        set { if (value) SetLanguage(AppLanguage.En); }
    }

    public void SetLanguage(AppLanguage lang)
    {
        Loc.CurrentLanguage = lang;
        OnPropertyChanged(nameof(Loc));
        OnPropertyChanged(nameof(IsPtBrSelected));
        OnPropertyChanged(nameof(IsEnSelected));

        if (string.IsNullOrEmpty(IsoPath))
        {
            IsoFileName = Loc.NoIsoSelected;
        }

        if (State == ProcessState.Idle)
        {
            StatusMessage = Loc.ReadyToStart;
            CurrentStageText = Loc.WaitingSelection;
        }

        OnPropertyChanged(nameof(StateText));
        RaiseInspection();
    }

    public string StateText => State switch
    {
        ProcessState.Running => Loc.StateRunning,
        ProcessState.Completed => Loc.StateCompleted,
        ProcessState.Failed => Loc.StateFailed,
        ProcessState.Cancelled => Loc.StateCancelled,
        _ => Loc.StateIdle
    };

    public string IsoPath
    {
        get => _isoPath;
        set
        {
            // The ISO and output path must not change while a run is using them.
            if (IsProcessing) return;

            if (SetProperty(ref _isoPath, value))
            {
                UpdateIsoDetails();
            }
        }
    }

    public string IsoFileName
    {
        get => _isoFileName;
        set => SetProperty(ref _isoFileName, value);
    }

    public string IsoSizeText
    {
        get => _isoSizeText;
        set => SetProperty(ref _isoSizeText, value);
    }

    public bool IsIsoValid
    {
        get => _isIsoValid;
        set => SetProperty(ref _isIsoValid, value);
    }

    public bool IsInspecting
    {
        get => _isInspecting;
        private set
        {
            if (SetProperty(ref _isInspecting, value))
            {
                RaiseInspection();
                OnPropertyChanged(nameof(CanGoNext));
            }
        }
    }

    public string InspectionMessage => IsInspecting ? Loc.ReadingEditions : _inspectionError;

    public WimEdition? SelectedEdition
    {
        get => _selectedEdition;
        set
        {
            if (SetProperty(ref _selectedEdition, value))
            {
                OnPropertyChanged(nameof(CanGoNext));
                ((RelayCommand)StartProcessCommand).RaiseCanExecuteChanged();
                ((RelayCommand)NextStepCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public OutputMode OutputMode
    {
        get => _outputMode;
        set
        {
            if (SetProperty(ref _outputMode, value))
            {
                OnPropertyChanged(nameof(IsIsoOutputMode));
                OnPropertyChanged(nameof(IsUsbOutputMode));
                OnPropertyChanged(nameof(CanGoNext));
                ((RelayCommand)StartProcessCommand).RaiseCanExecuteChanged();
                ((RelayCommand)NextStepCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsIsoOutputMode
    {
        get => OutputMode == OutputMode.IsoFile;
        set { if (value) OutputMode = OutputMode.IsoFile; }
    }

    public bool IsUsbOutputMode
    {
        get => OutputMode == OutputMode.BootableUsb;
        set { if (value) OutputMode = OutputMode.BootableUsb; }
    }

    public string OutputIsoPath
    {
        get => _outputIsoPath;
        set
        {
            if (SetProperty(ref _outputIsoPath, value))
            {
                OnPropertyChanged(nameof(CanGoNext));
                ((RelayCommand)StartProcessCommand).RaiseCanExecuteChanged();
                ((RelayCommand)NextStepCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public UsbDriveItem? SelectedUsbDrive
    {
        get => _selectedUsbDrive;
        set
        {
            if (SetProperty(ref _selectedUsbDrive, value))
            {
                OnPropertyChanged(nameof(CanGoNext));
                ((RelayCommand)StartProcessCommand).RaiseCanExecuteChanged();
                ((RelayCommand)NextStepCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public ProcessState State
    {
        get => _state;
        set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsProcessing));
                OnPropertyChanged(nameof(CanEditConfig));
                OnPropertyChanged(nameof(StateText));
                NotifyFooterButtons();
                ((RelayCommand)StartProcessCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CancelProcessCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsProcessing => State == ProcessState.Running;
    public bool CanEditConfig => State != ProcessState.Running;

    public int ProgressPercentage
    {
        get => _progressPercentage;
        set
        {
            if (SetProperty(ref _progressPercentage, value))
            {
                TrayStatusUpdated?.Invoke($"Kiso11: {CurrentStageText} ({value}%)");
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string CurrentStageText
    {
        get => _currentStageText;
        set => SetProperty(ref _currentStageText, value);
    }

    public string ElapsedTimeFormatted
    {
        get => _elapsedTimeFormatted;
        set => SetProperty(ref _elapsedTimeFormatted, value);
    }

    public string EstimatedRemainingFormatted
    {
        get => _estimatedRemainingFormatted;
        set
        {
            if (SetProperty(ref _estimatedRemainingFormatted, value))
            {
                OnPropertyChanged(nameof(RemainingTimeFormatted));
            }
        }
    }

    public string RemainingTimeFormatted => EstimatedRemainingFormatted;

    public string LogText
    {
        get => _logText;
        set
        {
            if (SetProperty(ref _logText, value))
            {
                OnPropertyChanged(nameof(Logs));
            }
        }
    }

    public string Logs => LogText;

    #endregion

    #region Commands

    public RelayCommand BrowseIsoCommand { get; }
    public RelayCommand BrowseOutputIsoCommand { get; }
    public RelayCommand RefreshUsbDrivesCommand { get; }
    public RelayCommand SelectPresetRecommendedCommand { get; }
    public RelayCommand StartProcessCommand { get; }
    public RelayCommand CancelProcessCommand { get; }
    public RelayCommand CopyLogCommand { get; }
    public RelayCommand ClearLogCommand { get; }

    #endregion

    /// Fills the view model with fixed values for the render preview. It starts no processes.
    internal void ApplyPreview(string isoPath, string fileName, string sizeText, IEnumerable<WimEdition> editions)
    {
        _isoPath = isoPath;
        OnPropertyChanged(nameof(IsoPath));
        IsoFileName = fileName;
        IsoSizeText = sizeText;

        Editions.Clear();
        foreach (var edition in editions)
        {
            Editions.Add(edition);
        }

        SelectedEdition = Editions.FirstOrDefault(IsPreferredEdition) ?? Editions.FirstOrDefault();
        IsIsoValid = SelectedEdition != null;
        OnPropertyChanged(nameof(HasIsoSelected));
        OnPropertyChanged(nameof(CanGoNext));
    }

    private void NotifyFooterButtons()
    {
        OnPropertyChanged(nameof(ShowNextButton));
        OnPropertyChanged(nameof(ShowStartButton));
        OnPropertyChanged(nameof(ShowCancelButton));
        OnPropertyChanged(nameof(ShowNewRunButton));
    }

    private void RaiseInspection()
    {
        OnPropertyChanged("InspectionMessage");
        OnPropertyChanged(nameof(IsInspectionError));
    }

    private void BrowseIso()
    {
        if (IsProcessing) return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.BrowseIso,
            Filter = "ISO (*.iso)|*.iso|*.*|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            IsoPath = dialog.FileName;
        }
    }

    private void BrowseOutputIso()
    {
        if (IsProcessing) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = Loc.SaveTo,
            Filter = "ISO (*.iso)|*.iso",
            FileName = "Windows11_Kiso11_Debloated.iso"
        };

        if (dialog.ShowDialog() == true)
        {
            OutputIsoPath = dialog.FileName;
        }
    }

    public void RefreshUsbDrives()
    {
        var currentSelected = SelectedUsbDrive?.DriveLetter;
        UsbDrives.Clear();

        foreach (var d in _usbService.GetAvailableUsbDrives())
        {
            UsbDrives.Add(d);
        }

        if (!string.IsNullOrEmpty(currentSelected))
        {
            SelectedUsbDrive = UsbDrives.FirstOrDefault(d => d.DriveLetter.Equals(currentSelected, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedUsbDrive is not { IsSupported: true })
        {
            SelectedUsbDrive = UsbDrives.FirstOrDefault(d => d.IsSupported);
        }

        ((RelayCommand)StartProcessCommand).RaiseCanExecuteChanged();
    }

    private void SelectPresetRecommended()
    {
        DebloatOptions.ApplyRecommended();
    }

    private void UpdateIsoDetails()
    {
        _inspectCts?.Cancel();
        Editions.Clear();
        SelectedEdition = null;
        IsIsoValid = false;
        _inspectionError = string.Empty;
        RaiseInspection();
        OnPropertyChanged(nameof(HasIsoSelected));

        if (File.Exists(IsoPath))
        {
            var info = new FileInfo(IsoPath);
            IsoFileName = info.Name;
            double gb = info.Length / (1024.0 * 1024 * 1024);
            IsoSizeText = $"{gb:0.00} GB";

            var dir = Path.GetDirectoryName(IsoPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var baseName = Path.GetFileNameWithoutExtension(IsoPath);
            OutputIsoPath = Path.Combine(dir, $"{baseName}_Kiso11.iso");

            _ = InspectIsoAsync(IsoPath);
        }
        else
        {
            IsoFileName = Loc.FileNotFound;
            IsoSizeText = "-";
            _inspectionError = Loc.FileNotFound;
            RaiseInspection();
        }

        OnPropertyChanged(nameof(CanGoNext));
        ((RelayCommand)NextStepCommand).RaiseCanExecuteChanged();
    }

    /// Reads the editions before the user can continue, so an invalid ISO is caught at step 1.
    private async Task InspectIsoAsync(string path)
    {
        var cts = new CancellationTokenSource();
        _inspectCts = cts;
        IsInspecting = true;

        try
        {
            var info = await _isoService.InspectAsync(path, cts.Token);
            if (cts.IsCancellationRequested) return;

            if (!info.IsWindowsInstaller)
            {
                _inspectionError = Loc.ErrNoInstallImage;
            }
            else
            {
                foreach (var edition in info.Editions)
                {
                    Editions.Add(edition);
                }

                SelectedEdition = Editions.FirstOrDefault(IsPreferredEdition) ?? Editions.FirstOrDefault();
                IsIsoValid = SelectedEdition != null;

                if (!IsIsoValid)
                {
                    _inspectionError = Loc.ErrNoEditions;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // A newer ISO replaced this one.
        }
        catch (Exception ex)
        {
            _inspectionError = ex.Message;
        }
        finally
        {
            if (_inspectCts == cts)
            {
                IsInspecting = false;
                RaiseInspection();
                OnPropertyChanged(nameof(CanGoNext));
                ((RelayCommand)NextStepCommand).RaiseCanExecuteChanged();
                ((RelayCommand)StartProcessCommand).RaiseCanExecuteChanged();
            }

            cts.Dispose();
        }
    }

    private static bool IsPreferredEdition(WimEdition edition) =>
        edition.Name.Equals("Windows 11 Pro", StringComparison.OrdinalIgnoreCase) ||
        edition.Name.Equals("Windows 10 Pro", StringComparison.OrdinalIgnoreCase);

    private bool CanStartProcess()
    {
        if (State == ProcessState.Running) return false;
        if (!IsIsoValid || IsInspecting || SelectedEdition == null || !File.Exists(IsoPath)) return false;

        if (OutputMode == OutputMode.BootableUsb)
        {
            return SelectedUsbDrive is { IsSupported: true };
        }

        return !string.IsNullOrWhiteSpace(OutputIsoPath);
    }

    private async Task StartProcessAsync()
    {
        if (!CanStartProcess()) return;

        if (!IsAdministrator)
        {
            if (Dialogs.Confirm(Loc.AdminRequiredTitle, Loc.AdminRequiredPrompt, Loc.Restart, Loc.Cancel))
            {
                RestartAsAdmin();
            }
            return;
        }

        var usbDrive = OutputMode == OutputMode.BootableUsb ? SelectedUsbDrive : null;
        if (usbDrive != null)
        {
            var confirmed = Dialogs.Confirm(
                Loc.UsbFormatConfirmTitle,
                Loc.UsbFormatConfirm(usbDrive.DriveLetter, usbDrive.VolumeLabel, usbDrive.TotalSizeFormatted),
                Loc.FormatAndWrite,
                Loc.Cancel,
                danger: true);

            if (!confirmed) return;
        }

        var edition = SelectedEdition!;
        var isoOutput = OutputIsoPath;
        var isoSource = IsoPath;

        State = ProcessState.Running;
        CurrentStep = 4;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        ClearLog();
        ProgressPercentage = 0;
        ElapsedTimeFormatted = "00:00";
        EstimatedRemainingFormatted = "--:--";
        CurrentStageText = Loc.Stage1Preparing;
        StatusMessage = Loc.Stage1Preparing;
        _stopwatch.Restart();
        _timer.Start();

        var workDir = Path.Combine(Path.GetTempPath(), $"Kiso11_Build_{Guid.NewGuid():N}");
        var mountDir = Path.Combine(workDir, "mount");
        var extractedDir = Path.Combine(workDir, "extracted");
        var bootMountDir = Path.Combine(workDir, "boot_mount");

        var result = ProcessState.Completed;
        string? errorMessage = null;

        try
        {
            Log(string.Format(Loc.LogStarting, isoSource));
            Log(string.Format(Loc.LogEdition, edition.Name));
            Log(usbDrive != null
                ? string.Format(Loc.LogOutputUsb, usbDrive.DriveLetter)
                : string.Format(Loc.LogOutputIso, isoOutput));

            // 1. Extract the ISO
            CurrentStageText = Loc.Stage1Extracting;
            ProgressPercentage = 5;
            StatusMessage = Loc.Stage1MountingIsoMsg;
            var imageIndex = await _isoService.ExtractIsoAsync(isoSource, extractedDir, edition.Index, (p, msg) =>
            {
                ProgressPercentage = p;
                StatusMessage = msg;
            }, token);

            var installWim = Path.Combine(extractedDir, "sources", "install.wim");
            if (!File.Exists(installWim))
            {
                throw new FileNotFoundException(Loc.ErrInstallWimMissing);
            }

            // 2. Mount the image
            CurrentStageText = Loc.Stage2Mounting;
            ProgressPercentage = 25;
            StatusMessage = Loc.Stage2MountingMsg;
            Log(Loc.LogMounting);
            await _dismService.MountWimAsync(installWim, imageIndex, mountDir, token);

            // 3. Apps and AI
            if (DebloatOptions.RemoveBloatwareApps)
            {
                CurrentStageText = Loc.Stage3Debloating;
                Log(Loc.LogRemovingApps);
                await _dismService.RemoveBloatwarePackagesAsync(mountDir, (p, msg) =>
                {
                    ProgressPercentage = 25 + (int)(p * 0.15);
                    StatusMessage = msg;
                }, token);
            }

            if (DebloatOptions.RemoveAiAndCopilot)
            {
                CurrentStageText = Loc.Stage3AiRemoval;
                Log(Loc.LogRemovingAi);
                await _dismService.RemoveAiComponentsAsync(mountDir, (_, msg) => StatusMessage = msg, token);
            }

            if (DebloatOptions.RemoveOneDrive)
            {
                Log(Loc.LogRemovingOneDrive);
                await _dismService.RemoveOneDriveFilesAsync(mountDir, token);
            }

            if (DebloatOptions.RemoveEdge)
            {
                Log(Loc.LogRemovingEdge);
                await _dismService.RemoveEdgeAsync(mountDir, token);
            }

            // 4. Registry tweaks on the offline hives
            CurrentStageText = Loc.Stage3Registry;
            ProgressPercentage = 50;
            StatusMessage = Loc.Stage3RegistryMsg;
            Log(Loc.LogRegistry);
            await _dismService.ApplyOfflineRegistryTweaksAsync(mountDir, DebloatOptions, token);

            // 5. Unattended answer file and setup bypasses
            Log(Loc.LogUnattend);
            _isoService.InjectAutounattendXml(extractedDir);

            if (DebloatOptions.BypassTpmAndHardware)
            {
                CurrentStageText = Loc.Stage5Bypasses;
                StatusMessage = Loc.Stage5BypassesMsg;
                Log(Loc.LogBypass);
                _isoService.PatchAppraiserResDll(extractedDir);
                await _dismService.ApplyBootWimBypassesAsync(Path.Combine(extractedDir, "sources", "boot.wim"), bootMountDir, token);
            }

            if (DebloatOptions.IntegrateIntelRstDrivers)
            {
                var driversDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Drivers");
                if (!Directory.Exists(driversDir))
                {
                    driversDir = Path.Combine(Directory.GetCurrentDirectory(), "Drivers");
                }

                if (Directory.Exists(driversDir))
                {
                    Log(string.Format(Loc.LogDrivers, driversDir));
                    await _dismService.IntegrateDriversAsync(mountDir, driversDir, token);
                }
                else
                {
                    Log(string.Format(Loc.LogDriversMissing, driversDir));
                }
            }

            // 6. Save and unmount
            CurrentStageText = Loc.Stage4Unmounting;
            ProgressPercentage = 65;
            StatusMessage = Loc.Stage4UnmountingMsg;
            Log(Loc.LogUnmounting);
            await _dismService.UnmountWimAsync(mountDir, commit: true, token);

            // 7. Export only the chosen edition
            ProgressPercentage = 75;
            StatusMessage = Loc.MsgExporting;
            Log(Loc.LogExporting);
            await _dismService.OptimizeAndExportWimAsync(installWim, imageIndex, DebloatOptions.CompressEsd, token);

            // 8. Write the output
            if (usbDrive != null)
            {
                CurrentStageText = Loc.Stage6WritingUsb;
                Log(string.Format(Loc.LogWritingUsb, usbDrive.DriveLetter));
                await _usbService.CreateBootableUsbAsync(extractedDir, usbDrive.DriveLetter, (p, msg) =>
                {
                    ProgressPercentage = 80 + (int)(p * 0.20);
                    StatusMessage = msg;
                }, token);
            }
            else
            {
                CurrentStageText = Loc.Stage6FinalizingIso;
                ProgressPercentage = 85;
                StatusMessage = Loc.MsgBuildingIso;
                Log(string.Format(Loc.LogBuildingIso, isoOutput));

                var isoBuilt = await _oscdimgService.BuildIsoAsync(extractedDir, isoOutput, "KISO11", token);
                if (!isoBuilt)
                {
                    throw new InvalidOperationException(Loc.ErrOscdimgBuild);
                }
            }

            ProgressPercentage = 100;
        }
        catch (OperationCanceledException)
        {
            result = ProcessState.Cancelled;
        }
        catch (Exception ex)
        {
            result = ProcessState.Failed;
            errorMessage = ex.Message;
            Log(string.Format(Loc.LogError, ex.Message));
        }
        finally
        {
            _timer.Stop();
            _stopwatch.Stop();

            // Unmount anything left behind and remove the work folder.
            CurrentStageText = Loc.StageCleanup;
            StatusMessage = Loc.StageCleanup;
            await _dismService.CleanupMountDirAsync(mountDir);
            try
            {
                if (Directory.Exists(workDir)) Directory.Delete(workDir, recursive: true);
            }
            catch { }
        }

        State = result;

        switch (result)
        {
            case ProcessState.Completed:
                CurrentStageText = Loc.StateCompleted;
                StatusMessage = Loc.ProcessFinishedSuccess;
                if (usbDrive != null)
                {
                    Log(string.Format(Loc.LogUsbDone, usbDrive.DriveLetter));
                }
                else
                {
                    Log(string.Format(Loc.LogIsoDone, isoOutput));
                }

                TrayCompleted?.Invoke();
                if (usbDrive != null)
                {
                    Dialogs.Info(Loc.SuccessTitle, Loc.SuccessUsbCreated(usbDrive.DriveLetter), Loc.Ok);
                }
                else
                {
                    ShowIsoCreatedDialog(isoOutput);
                }
                break;

            case ProcessState.Cancelled:
                CurrentStageText = Loc.StateCancelled;
                StatusMessage = Loc.LogCancelledByUser;
                Log(Loc.LogCancelledByUser);
                break;

            default:
                CurrentStageText = Loc.StateFailed;
                StatusMessage = string.Format(Loc.ErrGeneric, errorMessage);
                Dialogs.Info(Loc.ErrorTitle, Loc.ErrorBody(errorMessage ?? string.Empty), Loc.Ok);
                break;
        }
    }

    private void ShowIsoCreatedDialog(string path)
    {
        var choice = Dialogs.Show(
            Loc.SuccessTitle,
            Loc.SuccessIsoCreated(path),
            new DialogButton(Loc.ShowInExplorer, DialogButtonKind.Secondary),
            new DialogButton(Loc.Close, DialogButtonKind.Primary));

        if (choice == 0)
        {
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
    }

    private void CancelProcess()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            if (Dialogs.Confirm(Loc.CancelOperationTitle, Loc.CancelOperationMsg, Loc.CancelOperationConfirm, Loc.KeepRunning, danger: true))
            {
                _cts.Cancel();
                StatusMessage = Loc.StatusCancelling;
            }
        }
    }

    /// Cancels the running operation without asking, then waits until its cleanup has finished.
    public async Task CancelAndWaitAsync()
    {
        _cts?.Cancel();
        if (_runTask != null)
        {
            try
            {
                await _runTask;
            }
            catch { }
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var elapsed = _stopwatch.Elapsed;
        ElapsedTimeFormatted = FormatDuration(elapsed);

        // Estimate the remaining time from progress so far.
        if (ProgressPercentage > 5 && ProgressPercentage < 100)
        {
            var totalSeconds = elapsed.TotalSeconds;
            var estimatedTotal = totalSeconds / (ProgressPercentage / 100.0);
            var remaining = TimeSpan.FromSeconds(Math.Max(0, estimatedTotal - totalSeconds));
            EstimatedRemainingFormatted = "~" + FormatDuration(remaining);
        }
        else
        {
            EstimatedRemainingFormatted = "--:--";
        }
    }

    private static string FormatDuration(TimeSpan span) =>
        span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}"
            : $"{span.Minutes:D2}:{span.Seconds:D2}";

    private void OnLogLineReceived(string line)
    {
        Log(line);
    }

    private void Log(string message)
    {
        var timeStamp = DateTime.Now.ToString("HH:mm:ss");
        var formatted = $"[{timeStamp}] {message}";

        Application.Current?.Dispatcher?.InvokeAsync(() =>
        {
            _logBuffer.AppendLine(formatted);

            // Keep the newest half once the buffer passes the limit.
            if (_logBuffer.Length > LogBufferLimit)
            {
                var content = _logBuffer.ToString();
                var half = content.Length / 2;
                _logBuffer.Clear();
                _logBuffer.Append(content.Substring(half));
            }

            LogText = _logBuffer.ToString();
        });
    }

    private void CopyLog()
    {
        try
        {
            Clipboard.SetText(LogText);
            StatusMessage = Loc.LogCopied;
        }
        catch { }
    }

    private void ClearLog()
    {
        _logBuffer.Clear();
        LogText = string.Empty;
    }

    public void RestartAsAdmin()
    {
        try
        {
            var exePath = Environment.ProcessPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Kiso11.exe");
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(psi);
            Application.Current?.Shutdown();
        }
        catch (Exception ex)
        {
            Dialogs.Info(Loc.ErrorTitle, string.Format(Loc.ErrElevate, ex.Message), Loc.Ok);
        }
    }
}
