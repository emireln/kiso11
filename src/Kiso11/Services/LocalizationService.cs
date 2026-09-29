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
        ? "Otimizador de imagens do Windows & criador de mídia de instalação"
        : "Windows image optimizer & installation media creator";

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
        ? "A unidade selecionada será apagada e formatada em FAT32. Imagens WIM grandes serão divididas. Em unidades acima de 32 GB, será criada uma partição de inicialização FAT32 de 32 GB."
        : "The selected drive will be erased and formatted as FAT32. Large WIM images will be split. Drives larger than 32 GB will get a 32 GB FAT32 boot partition.";

    // Stepper Titles
    public string Step1Title => IsPtBr ? "Origem ISO" : "Source ISO";
    public string Step2Title => IsPtBr ? "Otimizações" : "Tweaks";
    public string Step3Title => IsPtBr ? "Destino" : "Destination";
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
    public string OptAiCopilot => IsPtBr ? "IA, Copilot e Recall" : "AI, Copilot, and Recall";
    public string OptBitlocker => IsPtBr ? "Desativar BitLocker Automático" : "Disable Automatic BitLocker";
    public string OptTelemetry => IsPtBr ? "Desativar Telemetria & Anúncios" : "Disable Telemetry & Ads";
    public string OptUserFolders => IsPtBr ? "Pastas de Usuário em 'Este PC'" : "User Folders in 'This PC'";
    public string OptOneDrive => IsPtBr ? "Remover OneDrive" : "Remove OneDrive";
    public string OptHardware => IsPtBr ? "Bypass TPM 2.0 / Secure Boot" : "Bypass TPM 2.0 / Secure Boot";
    public string OptMsa => IsPtBr ? "Bypass Conta Microsoft (Local)" : "Bypass Microsoft Account (Local)";
    public string OptDrivers => IsPtBr ? "Drivers Intel VMD / RST (NVMe)" : "Intel VMD / RST Drivers (NVMe)";
    public string OptEdge => IsPtBr ? "Remover Microsoft Edge" : "Remove Microsoft Edge";
    public string OptEsd => IsPtBr ? "Compressão Máxima ESD" : "Maximum ESD Compression";

    public string PageSourceTitle => IsPtBr ? "Escolha a ISO do Windows" : "Choose a Windows ISO";
    public string TitleBarSubtitle => IsPtBr ? "Criador de mídia de instalação" : "Windows installation media";
    public string BrowsePath => IsPtBr ? "Procurar…" : "Browse…";
    public string CloseWindowTooltip => IsPtBr ? "Fechar" : "Close";
    public string WorkflowTitle => IsPtBr ? "FLUXO DE TRABALHO" : "WORKFLOW";
    public string PageSourceSubtitle => IsPtBr ? "Vamos identificar as edições e preparar a imagem selecionada." : "We’ll identify the editions and prepare the image you choose.";
    public string SelectIsoAction => IsPtBr ? "Selecionar arquivo ISO" : "Choose ISO file";
    public string InspectIso => IsPtBr ? "Ler edições da ISO" : "Read ISO editions";
    public string InspectingIso => IsPtBr ? "Lendo a imagem…" : "Reading image…";
    public string IsoNeedsInspection => IsPtBr ? "Leia a ISO para ver as edições disponíveis." : "Read the ISO to see its available editions.";
    public string SupportedMediaNote => IsPtBr ? "Aceita ISOs com install.wim, install.esd ou partes install.swm." : "Supports ISOs containing install.wim, install.esd, or split install.swm files.";
    public string EditionsFound(int count) => IsPtBr ? $"{count} edição(ões) encontrada(s)." : $"Found {count} edition(s).";
    public string NoEditionsFound => IsPtBr ? "Nenhuma edição do Windows foi encontrada nesta ISO." : "No Windows editions were found in this ISO.";
    public string ChooseEdition => IsPtBr ? "Edição que será incluída" : "Edition to include";
    public string EditionOnlyNote => IsPtBr ? "A mídia gerada incluirá somente a edição selecionada." : "The output media will include only the selected edition.";
    public string PageCustomizeTitle => IsPtBr ? "Escolha o que mudar" : "Choose what to change";
    public string PageCustomizeSubtitle => IsPtBr ? "As opções são aplicadas à edição selecionada. Você pode manter o preset ou personalizar cada item." : "Options apply to the selected edition. Keep the preset or choose each change yourself.";
    public string RecommendedPresetDescription => IsPtBr ? "Uma configuração equilibrada para remover aplicativos promocionais e reduzir sugestões e telemetria." : "A balanced setup that removes promotional apps and reduces suggestions and telemetry.";
    public string PageOutputTitle => IsPtBr ? "Escolha como salvar" : "Choose where to save";
    public string PageOutputSubtitle => IsPtBr ? "Gere uma ISO para usar depois ou prepare um pendrive agora." : "Create an ISO to use later or prepare a USB drive now.";
    public string SaveIsoLabel => IsPtBr ? "Salvar ISO em" : "Save ISO to";
    public string NoUsbDrives => IsPtBr ? "Nenhuma unidade removível pronta foi encontrada." : "No ready removable drives were found.";
    public string PageProgressTitle => IsPtBr ? "Preparando sua mídia" : "Building your media";
    public string PageProgressSubtitle => IsPtBr ? "O Kiso11 está trabalhando na imagem selecionada." : "Kiso11 is working on the selected image.";
    public string LogTitle => IsPtBr ? "Detalhes da operação" : "Operation details";
    public string BloatwareDescription => IsPtBr ? "Remove aplicativos provisionados de consumo e recursos opcionais selecionados." : "Removes provisioned consumer apps and selected optional features.";
    public string AiDescription => IsPtBr ? "Remove pacotes de IA, Copilot e Recall quando existirem na imagem." : "Removes AI, Copilot, and Recall packages when they exist in the image.";
    public string BitlockerDescription => IsPtBr ? "Impede a criptografia automática do dispositivo durante a configuração inicial." : "Prevents automatic device encryption during initial setup.";
    public string TelemetryDescription => IsPtBr ? "Reduz coleta de diagnóstico, conteúdo sugerido e anúncios personalizados." : "Reduces diagnostic collection, suggested content, and tailored ads.";
    public string UserFoldersDescription => IsPtBr ? "Mostra Desktop, Documentos e outras pastas em Este Computador." : "Shows Desktop, Documents, and other folders under This PC.";
    public string OneDriveDescription => IsPtBr ? "Remove os instaladores do OneDrive da imagem." : "Removes OneDrive setup files from the image.";
    public string HardwareDescription => IsPtBr ? "Ignora verificações de TPM, Secure Boot, memória e CPU no instalador." : "Skips TPM, Secure Boot, memory, and CPU checks in Windows Setup.";
    public string MsaDescription => IsPtBr ? "Permite a opção de conta local durante a configuração do Windows." : "Enables the local account option during Windows setup.";
    public string DriversDescription => IsPtBr ? "Adiciona drivers Intel VMD/RST ao instalador e ao Windows instalado." : "Adds Intel VMD/RST drivers to Windows Setup and the installed image.";
    public string EdgeDescription => IsPtBr ? "Tenta remover componentes provisionados do Edge; alguns recursos do Windows podem depender dele." : "Attempts to remove provisioned Edge components; some Windows features may depend on it.";
    public string EsdDescription => IsPtBr ? "Usa compressão de recuperação para reduzir a imagem; pode demorar mais." : "Uses recovery compression to reduce image size; this can take longer.";
    public string OptionalLabel => IsPtBr ? "Opcional" : "Optional";
    public string PresetApplied => IsPtBr ? "Preset recomendado aplicado" : "Recommended settings applied";
    public string ApplyPreset => IsPtBr ? "Aplicar preset" : "Apply preset";
    public string AdministratorAccessNote => IsPtBr ? "É necessário executar como administrador para alterar imagens do Windows." : "Administrator access is required to service Windows images.";

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
    public string Stage3AiRemoval => IsPtBr ? "Etapa 3 de 6: Removendo componentes de IA e Copilot" : "Stage 3 of 6: Removing AI components and Copilot";
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
