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
using Microsoft.Win32;

namespace Kiso11.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly ProcessRunner _runner;
    private readonly IsoService _isoService;
    private readonly DismEngineService _dismService;
    private readonly UsbBootService _usbService;
    private readonly OscdimgService _oscdimgService;

    private CancellationTokenSource? _cts;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();

    private string _isoPath = string.Empty;
    private string _isoFileName = "Nenhuma ISO selecionada";
    private string _isoSizeText = "-";
    private bool _isIsoValid;

    private OutputMode _outputMode = OutputMode.IsoFile;
    private string _outputIsoPath = string.Empty;
    private UsbDriveItem? _selectedUsbDrive;

    private bool _isPresetRecommended = true;
    private bool _isCustomSettingsExpanded;
    private ProcessState _state = ProcessState.Idle;

    private int _progressPercentage;
    private string _statusMessage = "Pronto para iniciar.";
    private string _currentStageText = "Aguardando seleção";
    private string _elapsedTimeFormatted = "00:00";
    private string _estimatedRemainingFormatted = "--:--";

    private readonly StringBuilder _logBuffer = new();
    private string _logText = string.Empty;
    private bool _isLogExpanded = true;

    public MainViewModel()
    {
        _runner = new ProcessRunner();
        _isoService = new IsoService(_runner);
        _dismService = new DismEngineService(_runner);
        _usbService = new UsbBootService(_runner);
        _oscdimgService = new OscdimgService(_runner);

        _runner.LineReceived += OnLogLineReceived;

        UsbDrives = new ObservableCollection<UsbDriveItem>();
        DebloatOptions = new DebloatOptions();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;

        BrowseIsoCommand = new RelayCommand(BrowseIso);
        BrowseOutputIsoCommand = new RelayCommand(BrowseOutputIso);
        RefreshUsbDrivesCommand = new RelayCommand(RefreshUsbDrives);
        SelectPresetRecommendedCommand = new RelayCommand(SelectPresetRecommended);
        StartProcessCommand = new RelayCommand(async () => await StartProcessAsync(), () => CanStartProcess());
        CancelProcessCommand = new RelayCommand(CancelProcess, () => IsProcessing);
        CopyLogCommand = new RelayCommand(CopyLog);
        ClearLogCommand = new RelayCommand(ClearLog);
        RestartAsAdminCommand = new RelayCommand(RestartAsAdmin);
        SetLanguagePtBrCommand = new RelayCommand(() => SetLanguage(AppLanguage.PtBr));
        SetLanguageEnCommand = new RelayCommand(() => SetLanguage(AppLanguage.En));
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
        _statusMessage = Loc.StateIdle;
    }

    #region Properties

    public LocalizationService Loc => LocalizationService.Instance;
    public RelayCommand SetLanguagePtBrCommand { get; }
    public RelayCommand SetLanguageEnCommand { get; }
    public RelayCommand MinimizeToTrayCommand { get; }

    public RelayCommand NextStepCommand { get; }
    public RelayCommand PreviousStepCommand { get; }
    public RelayCommand StartNewDebloatCommand { get; }
    public RelayCommand GoToStepCommand { get; }

    private int _currentStep = 1;
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
                ((RelayCommand)NextStepCommand).RaiseCanExecuteChanged();
                ((RelayCommand)PreviousStepCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;

    public bool HasIsoSelected => IsIsoValid;

    public bool CanGoNext => CurrentStep switch
    {
        1 => IsIsoValid,
        2 => OutputMode == OutputMode.BootableUsb ? SelectedUsbDrive != null : !string.IsNullOrWhiteSpace(OutputIsoPath),
        3 => CanStartProcess(),
        _ => false
    };

    public bool CanGoBack => CurrentStep > 1 && CurrentStep < 4 && State != ProcessState.Running;

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
        if (step == 3 && (!IsIsoValid || (OutputMode == OutputMode.BootableUsb ? SelectedUsbDrive == null : string.IsNullOrWhiteSpace(OutputIsoPath)))) return;
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
        OnPropertyChanged(nameof(IsoFileName));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(CurrentStageText));

        if (string.IsNullOrEmpty(IsoPath))
        {
            IsoFileName = Loc.NoIsoSelected;
        }

        if (State == ProcessState.Idle)
        {
            StatusMessage = Loc.ReadyToStart;
            CurrentStageText = Loc.WaitingSelection;
        }
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
            if (SetProperty(ref _isoPath, value))
            {
                UpdateIsoDetails();
                ((RelayCommand)StartProcessCommand).RaiseCanExecuteChanged();
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
        set
        {
            if (SetProperty(ref _isIsoValid, value))
            {
                OnPropertyChanged(nameof(HasIsoSelected));
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

    public bool IsPresetRecommended
    {
        get => _isPresetRecommended;
        set
        {
            if (SetProperty(ref _isPresetRecommended, value) && value)
            {
                DebloatOptions.ApplyRecommended();
                NotifyDebloatOptionChanges();
            }
        }
    }

    public bool IsCustomSettingsExpanded
    {
        get => _isCustomSettingsExpanded;
        set => SetProperty(ref _isCustomSettingsExpanded, value);
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

    public bool IsLogExpanded
    {
        get => _isLogExpanded;
        set => SetProperty(ref _isLogExpanded, value);
    }

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

    private void BrowseIso()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecione a imagem ISO do Windows",
            Filter = "Imagens ISO (*.iso)|*.iso|Todos os arquivos (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            IsoPath = dialog.FileName;
        }
    }

    private void BrowseOutputIso()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Salvar ISO Debloated Como",
            Filter = "Imagem ISO (*.iso)|*.iso",
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

        var drives = _usbService.GetAvailableUsbDrives();
        foreach (var d in drives)
        {
            UsbDrives.Add(d);
        }

        if (!string.IsNullOrEmpty(currentSelected))
        {
            SelectedUsbDrive = UsbDrives.FirstOrDefault(d => d.DriveLetter.Equals(currentSelected, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedUsbDrive == null && UsbDrives.Count > 0)
        {
            SelectedUsbDrive = UsbDrives[0];
        }

        ((RelayCommand)StartProcessCommand).RaiseCanExecuteChanged();
    }

    private void SelectPresetRecommended()
    {
        _isPresetRecommended = true;
        OnPropertyChanged(nameof(IsPresetRecommended));
        DebloatOptions.ApplyRecommended();
        NotifyDebloatOptionChanges();
    }

    private void UpdateIsoDetails()
    {
        if (File.Exists(IsoPath))
        {
            var info = new FileInfo(IsoPath);
            IsoFileName = info.Name;
            double gb = info.Length / (1024.0 * 1024 * 1024);
            IsoSizeText = $"{gb:0.00} GB";
            IsIsoValid = true;

            var dir = Path.GetDirectoryName(IsoPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var baseName = Path.GetFileNameWithoutExtension(IsoPath);
            OutputIsoPath = Path.Combine(dir, $"{baseName}_Kiso11.iso");
        }
        else
        {
            IsoFileName = Loc.FileNotFound;
            IsoSizeText = "-";
            IsIsoValid = false;
        }

        OnPropertyChanged(nameof(CanGoNext));
        ((RelayCommand)NextStepCommand).RaiseCanExecuteChanged();
    }

    private bool CanStartProcess()
    {
        if (State == ProcessState.Running) return false;
        if (!IsIsoValid || !File.Exists(IsoPath)) return false;

        if (OutputMode == OutputMode.BootableUsb)
        {
            return SelectedUsbDrive != null;
        }

        return !string.IsNullOrWhiteSpace(OutputIsoPath);
    }

    private async Task StartProcessAsync()
    {
        if (!CanStartProcess()) return;

        if (!IsAdministrator)
        {
            var elevate = MessageBox.Show(
                Loc.AdminRequiredPrompt,
                Loc.AdminRequiredTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (elevate == MessageBoxResult.Yes)
            {
                RestartAsAdmin();
            }
            return;
        }

        if (OutputMode == OutputMode.BootableUsb && SelectedUsbDrive != null)
        {
            var confirm = MessageBox.Show(
                Loc.UsbFormatConfirm(SelectedUsbDrive.DriveLetter, SelectedUsbDrive.VolumeLabel, SelectedUsbDrive.TotalSizeFormatted),
                Loc.UsbFormatConfirmTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;
        }

        State = ProcessState.Running;
        CurrentStep = 4;
        _cts = new CancellationTokenSource();
        _stopwatch.Restart();
        _timer.Start();

        ProgressPercentage = 0;
        StatusMessage = Loc.ReadyToStart;
        CurrentStageText = Loc.Stage1Preparing;

        var workDir = Path.Combine(Path.GetTempPath(), $"Kiso11_Build_{Guid.NewGuid():N}");
        var mountDir = Path.Combine(workDir, "mount");
        var extractedDir = Path.Combine(workDir, "extracted");

        try
        {
            Log($"[Kiso11] Iniciando otimização da ISO: {IsoPath}");
            Log($"[Kiso11] Modo de saída: {(OutputMode == OutputMode.BootableUsb ? "Pendrive Bootável (" + SelectedUsbDrive?.DriveLetter + ")" : "Arquivo ISO (" + OutputIsoPath + ")")}");

            // 1. Extração da ISO
            CurrentStageText = Loc.Stage1Extracting;
            ProgressPercentage = 5;
            StatusMessage = Loc.Stage1MountingIsoMsg;
            await _isoService.ExtractIsoAsync(IsoPath, extractedDir, (p, msg) =>
            {
                ProgressPercentage = p;
                StatusMessage = msg;
            }, _cts.Token);

            var installWim = Path.Combine(extractedDir, "sources", "install.wim");
            if (!File.Exists(installWim))
            {
                throw new FileNotFoundException("install.wim não foi encontrado após a extração.");
            }

            // 2. Montagem do WIM
            CurrentStageText = Loc.Stage2Mounting;
            ProgressPercentage = 25;
            StatusMessage = Loc.Stage2MountingMsg;
            Log("[Kiso11] Montando install.wim...");
            await _dismService.MountWimAsync(installWim, 1, mountDir, _cts.Token);

            // 3. Remoção de Bloatware
            if (DebloatOptions.RemoveBloatwareApps)
            {
                CurrentStageText = Loc.Stage3Debloating;
                Log("[Kiso11] Removendo pacotes AppX inúteis...");
                await _dismService.RemoveBloatwarePackagesAsync(mountDir, (p, msg) =>
                {
                    ProgressPercentage = 25 + (int)(p * 0.15);
                    StatusMessage = msg;
                }, _cts.Token);
            }

            // 4. Remoção de IA e Copilot
            if (DebloatOptions.RemoveAiAndCopilot)
            {
                CurrentStageText = Loc.Stage3AiRemoval;
                Log("[Kiso11] Removendo Copilot, Recall e pacotes de IA...");
                await _dismService.RemoveAiComponentsAsync(mountDir, (p, msg) =>
                {
                    StatusMessage = msg;
                }, _cts.Token);
            }

            // Remoção de OneDrive e Edge se selecionados
            if (DebloatOptions.RemoveOneDrive)
            {
                Log("[Kiso11] Removendo arquivos de instalação do OneDrive...");
                await _dismService.RemoveOneDriveFilesAsync(mountDir, _cts.Token);
            }

            if (DebloatOptions.RemoveEdge)
            {
                Log("[Kiso11] Removendo Microsoft Edge...");
                await _dismService.RemoveEdgeAsync(mountDir, _cts.Token);
            }

            // 5. Otimizações de Registro Offline
            CurrentStageText = Loc.Stage3Registry;
            ProgressPercentage = 50;
            StatusMessage = Loc.Stage3RegistryMsg;
            Log("[Kiso11] Aplicando otimizações no registro offline...");
            await _dismService.ApplyOfflineRegistryTweaksAsync(mountDir, DebloatOptions, _cts.Token);

            // Injetar autounattend.xml
            Log("[Kiso11] Injetando autounattend.xml para bypass de conta online no OOBE...");
            _isoService.InjectAutounattendXml(extractedDir);

            // Injetar bypasses de hardware no boot.wim e appraiserres.dll se habilitado
            if (DebloatOptions.BypassTpmAndHardware)
            {
                CurrentStageText = Loc.Stage5Bypasses;
                StatusMessage = Loc.Stage5BypassesMsg;
                Log("[Kiso11] Configurando bypasses de TPM 2.0 / Secure Boot / RAM no boot.wim...");
                _isoService.PatchAppraiserResDll(extractedDir);

                var bootWim = Path.Combine(extractedDir, "sources", "boot.wim");
                var bootMount = Path.Combine(workDir, "boot_mount");
                await _dismService.ApplyBootWimBypassesAsync(bootWim, bootMount, _cts.Token);
            }

            // Drivers Intel VMD/RST
            if (DebloatOptions.IntegrateIntelDrivers)
            {
                var driversDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Drivers");
                if (!Directory.Exists(driversDir))
                {
                    driversDir = Path.Combine(Directory.GetCurrentDirectory(), "Drivers");
                }

                if (Directory.Exists(driversDir))
                {
                    Log($"[Kiso11] Integrando drivers Intel VMD/RST de {driversDir}...");
                    await _dismService.IntegrateDriversAsync(mountDir, driversDir, _cts.Token);
                }
            }

            // 6. Limpeza de componentes e desmontagem
            CurrentStageText = Loc.Stage4Unmounting;
            ProgressPercentage = 65;
            StatusMessage = Loc.Stage4UnmountingMsg;
            Log("[Kiso11] Desmontando e salvando imagem WIM...");
            await _dismService.UnmountWimAsync(mountDir, commit: true, _cts.Token);

            // Otimização e compressão do WIM
            ProgressPercentage = 75;
            StatusMessage = "Otimizando tamanho do arquivo de instalação...";
            Log("[Kiso11] Exportando imagem WIM limpa...");
            await _dismService.OptimizeAndExportWimAsync(installWim, DebloatOptions.CompressEsd, _cts.Token);

            // 7. Geração de Saída (ISO ou Pendrive)
            if (OutputMode == OutputMode.BootableUsb && SelectedUsbDrive != null)
            {
                CurrentStageText = Loc.Stage6WritingUsb;
                Log($"[Kiso11] Criando pendrive bootável em {SelectedUsbDrive.DriveLetter}...");
                await _usbService.CreateBootableUsbAsync(extractedDir, SelectedUsbDrive.DriveLetter, (p, msg) =>
                {
                    ProgressPercentage = 80 + (int)(p * 0.20);
                    StatusMessage = msg;
                }, _cts.Token);

                ProgressPercentage = 100;
                StatusMessage = Loc.ProcessFinishedSuccess;
                CurrentStageText = Loc.StateCompleted;
                Log($"[Kiso11] SUCESSO! Pendrive bootável pronto na unidade [{SelectedUsbDrive.DriveLetter}].");

                MessageBox.Show(
                    Loc.SuccessUsbCreated(SelectedUsbDrive.DriveLetter),
                    Loc.SuccessTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                CurrentStageText = Loc.Stage6FinalizingIso;
                ProgressPercentage = 85;
                StatusMessage = "Compilando ISO bootável UEFI com Oscdimg...";
                Log($"[Kiso11] Gerando arquivo ISO em: {OutputIsoPath}...");

                var isoBuilt = await _oscdimgService.BuildIsoAsync(extractedDir, OutputIsoPath, "KISO11", _cts.Token);
                if (!isoBuilt)
                {
                    throw new InvalidOperationException("Falha ao gerar o arquivo ISO com Oscdimg.");
                }

                ProgressPercentage = 100;
                StatusMessage = Loc.ProcessFinishedSuccess;
                CurrentStageText = Loc.StateCompleted;
                Log($"[Kiso11] SUCESSO! Arquivo salvo em: {OutputIsoPath}");

                MessageBox.Show(
                    Loc.SuccessIsoCreated(OutputIsoPath),
                    Loc.SuccessTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            State = ProcessState.Completed;
            TrayCompleted?.Invoke();
        }
        catch (OperationCanceledException)
        {
            State = ProcessState.Cancelled;
            StatusMessage = "Operação cancelada pelo usuário.";
            CurrentStageText = "Cancelado";
            Log("[Kiso11] Operação cancelada pelo usuário.");
        }
        catch (Exception ex)
        {
            State = ProcessState.Failed;
            StatusMessage = $"Erro: {ex.Message}";
            CurrentStageText = "Falha";
            Log($"[Kiso11 ERRO] {ex.Message}\n{ex.StackTrace}");

            MessageBox.Show(
                $"Ocorreu um erro durante o processo:\n\n{ex.Message}\n\nConsulte o console de logs para detalhes.",
                "Erro no Kiso11",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _timer.Stop();
            _stopwatch.Stop();

            // Desmonta e limpa qualquer diretório temporário
            CurrentStageText += " (Limpando temporários...)";
            await _dismService.CleanupMountDirAsync(mountDir);
            try
            {
                if (Directory.Exists(workDir)) Directory.Delete(workDir, recursive: true);
            }
            catch { }
        }
    }

    private void CancelProcess()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            var result = MessageBox.Show(
                "Deseja realmente cancelar a operação em andamento?\nImagens montadas serão descartadas com segurança.",
                "Cancelar Kiso11",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _cts.Cancel();
                StatusMessage = "Cancelando operação... aguarde a limpeza.";
            }
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var elapsed = _stopwatch.Elapsed;
        ElapsedTimeFormatted = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";

        // Estimate remaining time based on progress
        if (ProgressPercentage > 5 && ProgressPercentage < 100)
        {
            var totalSeconds = elapsed.TotalSeconds;
            var estimatedTotal = totalSeconds / (ProgressPercentage / 100.0);
            var remaining = TimeSpan.FromSeconds(Math.Max(0, estimatedTotal - totalSeconds));
            EstimatedRemainingFormatted = $"~{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }
        else
        {
            EstimatedRemainingFormatted = "--:--";
        }
    }

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

            // Limit buffer size to ~2000 lines
            if (_logBuffer.Length > 200000)
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
            MessageBox.Show("Logs copiados para a área de transferência.", "Kiso11", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch { }
    }

    private void ClearLog()
    {
        _logBuffer.Clear();
        LogText = string.Empty;
    }

    private void NotifyDebloatOptionChanges()
    {
        OnPropertyChanged(nameof(DebloatOptions));
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
            MessageBox.Show($"Não foi possível elevar o processo: {ex.Message}", "Kiso11", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
