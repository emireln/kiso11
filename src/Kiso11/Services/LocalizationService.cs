using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Kiso11.Services;

public enum AppLanguage
{
    PtBr,
    En
}

public class LocalizationService : INotifyPropertyChanged
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    private AppLanguage _currentLanguage;

    public LocalizationService()
    {
        var currentCulture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        _currentLanguage = currentCulture == "pt" ? AppLanguage.PtBr : AppLanguage.En;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public AppLanguage CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                OnPropertyChanged(null); // Notify all properties changed
            }
        }
    }

    public bool IsPtBr => CurrentLanguage == AppLanguage.PtBr;
    public bool IsEn => CurrentLanguage == AppLanguage.En;

    // Header
    public string AppSubtitle => IsPtBr
        ? "Otimizador de ISO do Windows 11 (24H2/23H2) & Criador de Pendrive Bootável"
        : "Windows 11 (24H2/23H2) ISO Debloater & Bootable USB Creator";

    public string AdminActive => IsPtBr ? "Administrador Ativo" : "Administrator Active";
    public string RestartAsAdmin => IsPtBr ? "Reiniciar como Administrador" : "Restart as Administrator";

    // Card 1
    public string Card1Title => IsPtBr ? "1. Imagem de Origem (ISO do Windows 10/11)" : "1. Source Image (Windows 10/11 ISO)";
    public string BrowseIso => IsPtBr ? "Procurar ISO..." : "Browse ISO...";
    public string FileLabel => IsPtBr ? "Arquivo: " : "File: ";
    public string SizeLabel => IsPtBr ? "Tamanho: " : "Size: ";
    public string IsoLoaded => IsPtBr ? "✓ ISO Carregada" : "✓ ISO Loaded";
    public string SelectIso => IsPtBr ? "Selecione uma ISO" : "Select an ISO";
    public string NoIsoSelected => IsPtBr ? "Nenhuma ISO selecionada" : "No ISO selected";

    // Card 2
    public string Card2Title => IsPtBr ? "2. Destino do Windows Otimizado" : "2. Target Destination";
    public string RadioIso => IsPtBr ? "Gerar Arquivo ISO (.iso)" : "Generate ISO File (.iso)";
    public string RadioUsb => IsPtBr ? "Gravar em Pendrive Bootável (USB)" : "Write to Bootable USB Drive";
    public string SaveAs => IsPtBr ? "Salvar Como..." : "Save As...";
    public string IsoHint => IsPtBr
        ? "Gera um arquivo ISO debloated inicializável UEFI, pronto para gravação com Rufus, Ventoy ou máquinas virtuais."
        : "Generates a debloated UEFI bootable ISO file, ready for Rufus, Ventoy, or virtual machines.";
    public string RefreshDrives => IsPtBr ? "Atualizar" : "Refresh";
    public string RefreshTooltip => IsPtBr ? "Atualizar lista de pendrives" : "Refresh USB drives list";
    public string UsbWarning => IsPtBr
        ? "O pendrive será formatado como FAT32 UEFI compatível com 100% dos computadores. Arquivos de instalação maiores que 4GB serão divididos automaticamente (DISM Split-Image) em partes SWM."
        : "The USB drive will be formatted as FAT32 UEFI compatible with 100% of PCs. Installation files larger than 4GB will be automatically split (DISM Split-Image) into SWM parts.";

    // Stepper Titles
    public string Step1Title => IsPtBr ? "Origem ISO" : "Source ISO";
    public string Step2Title => IsPtBr ? "Destino" : "Destination";
    public string Step3Title => IsPtBr ? "Otimizações" : "Tweaks";
    public string Step4Title => IsPtBr ? "Execução" : "Build";

    // Stepper Navigation
    public string Next => IsPtBr ? "Avançar" : "Next";
    public string Back => IsPtBr ? "Voltar" : "Back";
    public string DropIsoHere => IsPtBr ? "Arraste e solte o arquivo ISO aqui ou clique para procurar" : "Drag and drop Windows ISO here or click to browse";
    public string IsoFileDetected => IsPtBr ? "Arquivo ISO Carregado" : "ISO File Loaded";
    public string ChooseDifferentIso => IsPtBr ? "Trocar ISO" : "Change ISO";
    public string TargetIsoTitle => IsPtBr ? "Arquivo ISO" : "ISO Image";
    public string TargetIsoDesc => IsPtBr ? "Gerar arquivo .iso debloated para instalação limpa ou máquina virtual" : "Generate debloated .iso file for clean install or virtual machine";
    public string TargetUsbTitle => IsPtBr ? "Pendrive USB" : "Bootable USB";
    public string TargetUsbDesc => IsPtBr ? "Formatar e gravar pendrive bootável UEFI com divisão automática de WIM" : "Format and write bootable UEFI USB drive with auto-split WIM";
    public string PresetRecommended => IsPtBr ? "Preset Recomendado" : "Recommended Preset";
    public string StartDebloat => IsPtBr ? "Iniciar Otimização" : "Start Optimization";
    public string StartNew => IsPtBr ? "Nova Otimização" : "New Debloat";
    public string SelectTweaksTitle => IsPtBr ? "Selecione as Otimizações Desejadas:" : "Select Desired Optimizations:";
    public string SelectUsbDriveTitle => IsPtBr ? "Selecione a Unidade USB:" : "Select USB Drive:";
    public string SaveIsoAsTitle => IsPtBr ? "Salvar ISO Debloated Como:" : "Save Debloated ISO As:";

    // Concise Options with clean labels
    public string OptBloatware => IsPtBr ? "Bloatware Apps (Loja, Jogos)" : "Bloatware Apps (Store, Games)";
    public string OptAiCopilot => IsPtBr ? "IA, Copilot & Recall (24H2)" : "AI, Copilot & Recall (24H2)";
    public string OptBitlocker => IsPtBr ? "Desativar BitLocker Automático" : "Disable Automatic BitLocker";
    public string OptTelemetry => IsPtBr ? "Desativar Telemetria & Anúncios" : "Disable Telemetry & Ads";
    public string OptUserFolders => IsPtBr ? "Pastas de Usuário em 'Este PC'" : "User Folders in 'This PC'";
    public string OptOneDrive => IsPtBr ? "Remover OneDrive" : "Remove OneDrive";
    public string OptHardware => IsPtBr ? "Bypass TPM 2.0 / Secure Boot" : "Bypass TPM 2.0 / Secure Boot";
    public string OptMsa => IsPtBr ? "Bypass Conta Microsoft (Local)" : "Bypass Microsoft Account (Local)";
    public string OptDrivers => IsPtBr ? "Drivers Intel VMD / RST (NVMe)" : "Intel VMD / RST Drivers (NVMe)";
    public string OptEdge => IsPtBr ? "Remover Microsoft Edge" : "Remove Microsoft Edge";
    public string OptEsd => IsPtBr ? "Compressão Máxima ESD" : "Maximum ESD Compression";

    // Window
    public string WindowTitle => IsPtBr
        ? "Kiso11 - Otimizador de ISO do Windows 11 & Pendrive Bootável"
        : "Kiso11 - Windows 11 ISO Debloater & Bootable USB Creator";

    // Dialogs
    public string AdminRequiredTitle => IsPtBr ? "Elevação Necessária" : "Elevation Required";
    public string AdminRequiredPrompt => IsPtBr
        ? "O Kiso11 requer privilégios de Administrador para manipular imagens com o DISM e gravar pendrives.\n\nDeseja reiniciar o aplicativo agora com privilégios de Administrador?"
        : "Kiso11 requires Administrator privileges to service images with DISM and write USB drives.\n\nDo you want to restart the application now as Administrator?";

    public string UsbFormatConfirmTitle => IsPtBr ? "Confirmação de Formatação de Pendrive" : "USB Drive Format Confirmation";
    public string UsbFormatConfirm(string letter, string label, string size) => IsPtBr
        ? $"ATENÇÃO: A criação do pendrive bootável irá FORMATAR a unidade [{letter}] ({label} - {size}).\n\nTodos os dados contidos neste pendrive serão APAGADOS permanentemente.\n\nDeseja continuar?"
        : $"WARNING: Bootable USB creation will FORMAT drive [{letter}] ({label} - {size}).\n\nAll existing data on this drive will be PERMANENTLY ERASED.\n\nDo you want to proceed?";

    public string SuccessTitle => IsPtBr ? "Sucesso!" : "Success!";
    public string SuccessIsoCreated(string path) => IsPtBr
        ? $"ISO debloated gerada com sucesso!\n\nLocalização:\n{path}"
        : $"Debloated ISO created successfully!\n\nLocation:\n{path}";
    public string SuccessUsbCreated(string letter) => IsPtBr
        ? $"Pendrive bootável UEFI criado com sucesso na unidade {letter}!"
        : $"UEFI Bootable USB drive successfully created on drive {letter}!";

    // Stages
    public string ReadyToStart => IsPtBr ? "Pronto para iniciar." : "Ready to start.";
    public string WaitingSelection => IsPtBr ? "Aguardando seleção de ISO" : "Waiting for ISO selection";
    public string FileNotFound => IsPtBr ? "Arquivo não encontrado" : "File not found";
    public string Stage1Preparing => IsPtBr ? "Etapa 1 de 6: Preparando ambiente" : "Stage 1 of 6: Preparing workspace";
    public string Stage1Extracting => IsPtBr ? "Etapa 1 de 6: Extraindo arquivos da ISO" : "Stage 1 of 6: Extracting ISO files";
    public string Stage1MountingIsoMsg => IsPtBr ? "Montando e copiando arquivos da imagem ISO..." : "Mounting and copying ISO image files...";
    public string Stage2Mounting => IsPtBr ? "Etapa 2 de 6: Montando imagem de instalação (install.wim)" : "Stage 2 of 6: Mounting installation image (install.wim)";
    public string Stage2MountingMsg => IsPtBr ? "Montando install.wim com DISM..." : "Mounting install.wim with DISM...";
    public string Stage3Debloating => IsPtBr ? "Etapa 3 de 6: Removendo aplicativos e bloatware" : "Stage 3 of 6: Removing apps and bloatware";
    public string Stage3AiRemoval => IsPtBr ? "Etapa 3 de 6: Removendo componentes de IA e Copilot (24H2/23H2)" : "Stage 3 of 6: Removing AI components & Copilot (24H2/23H2)";
    public string Stage3Registry => IsPtBr ? "Etapa 3 de 6: Aplicando tweaks de registro offline" : "Stage 3 of 6: Applying offline registry tweaks";
    public string Stage3RegistryMsg => IsPtBr ? "Modificando chaves de registro do Windows offline..." : "Modifying offline Windows registry hives...";
    public string Stage4Unmounting => IsPtBr ? "Etapa 4 de 6: Salvando e desmontando imagem" : "Stage 4 of 6: Committing and unmounting image";
    public string Stage4UnmountingMsg => IsPtBr ? "Desmontando install.wim e salvando alterações..." : "Unmounting install.wim and saving changes...";
    public string Stage5Bypasses => IsPtBr ? "Etapa 5 de 6: Injetando bypass de requisitos (LabConfig)" : "Stage 5 of 6: Injecting hardware bypasses (LabConfig)";
    public string Stage5BypassesMsg => IsPtBr ? "Configurando boot.wim e autounattend.xml..." : "Configuring boot.wim and autounattend.xml...";
    public string Stage6FinalizingIso => IsPtBr ? "Etapa 6 de 6: Gerando ISO inicializável UEFI com Oscdimg" : "Stage 6 of 6: Creating bootable UEFI ISO with Oscdimg";
    public string Stage6WritingUsb => IsPtBr ? "Etapa 6 de 6: Formatando pendrive e gravando arquivos" : "Stage 6 of 6: Formatting USB drive and copying files";
    public string ProcessFinishedSuccess => IsPtBr ? "Processo concluído com sucesso!" : "Process completed successfully!";

    // System Tray
    public string TrayOpen => IsPtBr ? "Abrir Kiso11" : "Open Kiso11";
    public string TrayMinimize => IsPtBr ? "Minimizar para a Bandeja" : "Minimize to Tray";
    public string TrayStatus => IsPtBr ? "Status: " : "Status: ";
    public string TrayLanguage => IsPtBr ? "Idioma" : "Language";
    public string TrayExit => IsPtBr ? "Sair do Kiso11" : "Exit Kiso11";
    public string TrayMinimizedTitle => IsPtBr ? "Kiso11 Minimizado" : "Kiso11 Minimized";
    public string TrayMinimizedMsg => IsPtBr
        ? "O aplicativo continua em execução na bandeja do sistema. Clique para abrir."
        : "The application continues running in the system tray. Click to open.";
    public string TrayCompletedTitle => IsPtBr ? "Kiso11 - Concluído!" : "Kiso11 - Completed!";
    public string TrayCompletedMsg => IsPtBr
        ? "A operação de preparação da mídia foi concluída com sucesso!"
        : "The media creation process completed successfully!";

    public string ClosePromptRunningTitle => IsPtBr ? "Operação em Andamento" : "Operation in Progress";
    public string ClosePromptRunningMsg => IsPtBr
        ? "O Kiso11 está executando uma operação no momento.\n\nDeseja minimizar para a bandeja do sistema para continuar em segundo plano?\n\n(Clique em 'Não' para cancelar o processo e fechar o aplicativo)"
        : "Kiso11 is currently executing an operation.\n\nDo you want to minimize to the system tray to continue running in the background?\n\n(Click 'No' to cancel the operation and exit)";

    // Card 4
    public string StartOptimization => IsPtBr ? "Iniciar Otimização" : "Start Optimization";
    public string CancelOperation => IsPtBr ? "Cancelar Operação" : "Cancel Operation";
    public string MetricElapsed => IsPtBr ? "TEMPO DECORRIDO" : "ELAPSED TIME";
    public string MetricEstimated => IsPtBr ? "ESTIMATIVA RESTANTE" : "ESTIMATED REMAINING";
    public string MetricStatus => IsPtBr ? "ESTADO ATUAL" : "CURRENT STATUS";

    // States
    public string StateIdle => IsPtBr ? "Pronto" : "Ready";
    public string StateRunning => IsPtBr ? "Processando..." : "Processing...";
    public string StateCompleted => IsPtBr ? "Concluído" : "Completed";
    public string StateFailed => IsPtBr ? "Falha" : "Failed";
    public string StateCancelled => IsPtBr ? "Cancelado" : "Cancelled";

    // Card 5
    public string ConsoleTitle => IsPtBr ? "Console de Operações (Tempo Real)" : "Live Operation Console";
    public string CopyText => IsPtBr ? "Copiar" : "Copy";
    public string ClearText => IsPtBr ? "Limpar" : "Clear";
}
