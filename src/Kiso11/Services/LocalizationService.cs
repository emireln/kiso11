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

/// All user-facing text. Keep entries short; put detail in tooltips.
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
    public string WindowTitle => "Kiso11";
    public string AdminStatus => IsPtBr ? "Administrador" : "Administrator";
    public string RestartAsAdmin => IsPtBr ? "Executar como administrador" : "Run as administrator";
    public string MinimizeToTray => IsPtBr ? "Minimizar para a bandeja" : "Minimize to tray";
    public string TrayButton => IsPtBr ? "Bandeja" : "Tray";

    // Steps
    public string Step1Title => IsPtBr ? "Origem" : "Source";
    public string Step2Title => IsPtBr ? "Destino" : "Output";
    public string Step3Title => IsPtBr ? "Ajustes" : "Options";
    public string Step4Title => IsPtBr ? "Execução" : "Build";

    // Step 1: source ISO
    public string Step1Heading => IsPtBr ? "Imagem do Windows" : "Windows image";
    public string Step1Hint => IsPtBr ? "Windows 10 ou 11, 64 bits." : "Windows 10 or 11, 64-bit.";
    public string DropTitle => IsPtBr ? "Arraste uma ISO até aqui" : "Drop an ISO file here";
    public string DropOr => IsPtBr ? "ou" : "or";
    public string BrowseIso => IsPtBr ? "Escolher ISO" : "Choose ISO";
    public string ChangeIso => IsPtBr ? "Trocar" : "Change";
    public string IsoValid => IsPtBr ? "Válida" : "Valid";
    public string NoIsoSelected => IsPtBr ? "Nenhuma ISO selecionada" : "No ISO selected";
    public string FileNotFound => IsPtBr ? "Arquivo não encontrado." : "File not found.";
    public string EditionLabel => IsPtBr ? "Edição" : "Edition";
    public string ReadingEditions => IsPtBr ? "Lendo as edições da ISO…" : "Reading the ISO editions…";
    public string ErrNoInstallImage => IsPtBr ? "Esta ISO não contém install.wim nem install.esd." : "This ISO has no install.wim or install.esd.";
    public string ErrNoEditions => IsPtBr ? "Nenhuma edição encontrada nesta ISO." : "No editions were found in this ISO.";
    public string ErrMountIso => IsPtBr ? "Não foi possível montar a ISO. Verifique se o arquivo não está corrompido." : "Could not mount the ISO. Check that the file is not corrupted.";
    public string ErrEsdConvert => IsPtBr ? "Falha ao converter install.esd em install.wim." : "Could not convert install.esd to install.wim.";
    public string MsgMountingIso => IsPtBr ? "Montando a ISO…" : "Mounting ISO…";
    public string MsgCopyingIso => IsPtBr ? "Copiando arquivos da ISO…" : "Copying ISO files…";
    public string MsgConvertingEsd => IsPtBr ? "Convertendo install.esd em install.wim…" : "Converting install.esd to install.wim…";

    // Step 2: output
    public string Step2Heading => IsPtBr ? "Destino" : "Output";
    public string Step2Hint => IsPtBr ? "Escolha o formato do resultado." : "Choose the output format.";
    public string TargetIsoTitle => IsPtBr ? "Arquivo ISO" : "ISO file";
    public string TargetIsoDesc => IsPtBr ? "Cria um .iso inicializável (UEFI)." : "Creates a bootable UEFI .iso.";
    public string TargetUsbTitle => IsPtBr ? "Pendrive USB" : "USB drive";
    public string TargetUsbDesc => IsPtBr ? "Formata em FAT32 e grava (UEFI)." : "Formats as FAT32 and writes (UEFI).";
    public string SaveTo => IsPtBr ? "Salvar em" : "Save to";
    public string Browse => IsPtBr ? "Procurar" : "Browse";
    public string Drive => IsPtBr ? "Unidade" : "Drive";
    public string Refresh => IsPtBr ? "Atualizar" : "Refresh";
    public string UsbEraseWarning => IsPtBr ? "Todos os dados da unidade serão apagados." : "All data on the drive will be erased.";
    public string NoUsbDrives => IsPtBr ? "Nenhum pendrive encontrado. Conecte um e atualize." : "No USB drive found. Plug one in and refresh.";
    public string UsbNoLabel => IsPtBr ? "sem nome" : "no label";
    public string UsbTooLargeNote => IsPtBr ? "acima de 32 GB" : "over 32 GB";

    // Step 3: options
    public string Step3Heading => IsPtBr ? "Ajustes" : "Options";
    public string Step3Hint => IsPtBr ? "Escolha o que aplicar à imagem." : "Choose what to apply to the image.";
    public string Recommended => IsPtBr ? "Recomendado" : "Recommended";
    public string GroupRemove => IsPtBr ? "Remover" : "Remove";
    public string GroupSystem => IsPtBr ? "Sistema" : "System";
    public string GroupAdvanced => IsPtBr ? "Avançado" : "Advanced";

    public string OptBloatware => IsPtBr ? "Apps e bloatware" : "Bloatware apps";
    public string TipBloatware => IsPtBr ? "Remove apps promocionais e jogos pré-instalados." : "Removes promotional apps and preinstalled games.";
    public string OptAiCopilot => IsPtBr ? "IA, Copilot e Recall" : "AI, Copilot and Recall";
    public string TipAiCopilot => IsPtBr ? "Remove o Copilot, o Recall e os componentes de IA." : "Removes Copilot, Recall and the AI components.";
    public string OptOneDrive => IsPtBr ? "OneDrive" : "OneDrive";
    public string TipOneDrive => IsPtBr ? "Remove o instalador do OneDrive da imagem." : "Removes the OneDrive installer from the image.";
    public string OptEdge => IsPtBr ? "Microsoft Edge" : "Microsoft Edge";
    public string TipEdge => IsPtBr ? "Remove o Edge da imagem." : "Removes Microsoft Edge from the image.";

    public string OptBitlocker => IsPtBr ? "Bloquear BitLocker automático" : "Block automatic BitLocker";
    public string TipBitlocker => IsPtBr ? "Impede a criptografia automática do disco (PreventDeviceEncryption)." : "Stops automatic disk encryption (PreventDeviceEncryption).";
    public string OptTelemetry => IsPtBr ? "Telemetria e anúncios" : "Telemetry and ads";
    public string TipTelemetry => IsPtBr ? "Desativa diagnóstico, ID de anúncio e sugestões." : "Turns off diagnostics, advertising ID and suggestions.";
    public string OptUserFolders => IsPtBr ? "Pastas em Este PC" : "Folders in This PC";
    public string TipUserFolders => IsPtBr ? "Mantém Documentos, Downloads e as demais pastas visíveis." : "Keeps Documents, Downloads and the other folders visible.";
    public string OptMsa => IsPtBr ? "Conta local, sem Microsoft" : "Local account, no Microsoft";
    public string TipMsa => IsPtBr ? "Permite instalar com conta local, sem internet (BypassNRO)." : "Allows setup with a local account and no internet (BypassNRO).";
    public string OptHardware => IsPtBr ? "Ignorar TPM, Secure Boot e RAM" : "Skip TPM, Secure Boot and RAM checks";
    public string TipHardware => IsPtBr ? "Permite instalar em computadores fora dos requisitos mínimos." : "Allows installing on computers below the minimum requirements.";

    public string OptDrivers => IsPtBr ? "Drivers Intel VMD / RST" : "Intel VMD / RST drivers";
    public string TipDrivers => IsPtBr ? "Integra drivers de NVMe para Intel de 11ª a 14ª geração (pasta Drivers)." : "Adds NVMe storage drivers for Intel 11th to 14th gen (Drivers folder).";
    public string OptEsd => IsPtBr ? "Compressão máxima (ESD)" : "Maximum compression (ESD)";
    public string TipEsd => IsPtBr ? "Arquivo menor, porém a etapa demora mais." : "Smaller file, but the step takes longer.";

    // Step 4: build
    public string Elapsed => IsPtBr ? "Decorrido" : "Elapsed";
    public string Remaining => IsPtBr ? "Restante" : "Remaining";
    public string ConsoleTitle => IsPtBr ? "Log" : "Log";
    public string CopyLog => IsPtBr ? "Copiar log" : "Copy log";
    public string ClearLog => IsPtBr ? "Limpar log" : "Clear log";
    public string LogCopied => IsPtBr ? "Log copiado para a área de transferência." : "Log copied to the clipboard.";
    public string NewRun => IsPtBr ? "Nova execução" : "New run";

    // Buttons
    public string Back => IsPtBr ? "Voltar" : "Back";
    public string Continue => IsPtBr ? "Continuar" : "Continue";
    public string Start => IsPtBr ? "Iniciar" : "Start";
    public string Cancel => IsPtBr ? "Cancelar" : "Cancel";
    public string Close => IsPtBr ? "Fechar" : "Close";
    public string Ok => "OK";

    // States
    public string StateIdle => IsPtBr ? "Pronto" : "Ready";
    public string StateRunning => IsPtBr ? "Em andamento" : "Running";
    public string StateCompleted => IsPtBr ? "Concluído" : "Completed";
    public string StateFailed => IsPtBr ? "Falha" : "Failed";
    public string StateCancelled => IsPtBr ? "Cancelado" : "Cancelled";
    public string ReadyToStart => IsPtBr ? "Escolha uma ISO para começar." : "Choose an ISO to start.";
    public string WaitingSelection => IsPtBr ? "Aguardando a ISO" : "Waiting for an ISO";
    public string ProcessFinishedSuccess => IsPtBr ? "Concluído com sucesso." : "Completed successfully.";

    // Stages
    public string Stage1Preparing => IsPtBr ? "Preparando o ambiente" : "Preparing the workspace";
    public string Stage1Extracting => IsPtBr ? "Extraindo a ISO" : "Extracting the ISO";
    public string Stage1MountingIsoMsg => IsPtBr ? "Copiando os arquivos da imagem…" : "Copying the image files…";
    public string Stage2Mounting => IsPtBr ? "Montando install.wim" : "Mounting install.wim";
    public string Stage2MountingMsg => IsPtBr ? "Montando a imagem com o DISM…" : "Mounting the image with DISM…";
    public string Stage3Debloating => IsPtBr ? "Removendo apps" : "Removing apps";
    public string Stage3AiRemoval => IsPtBr ? "Removendo IA e Copilot" : "Removing AI and Copilot";
    public string Stage3Registry => IsPtBr ? "Aplicando ajustes de registro" : "Applying registry changes";
    public string Stage3RegistryMsg => IsPtBr ? "Alterando o registro offline do Windows…" : "Editing the offline Windows registry…";
    public string Stage4Unmounting => IsPtBr ? "Salvando a imagem" : "Saving the image";
    public string Stage4UnmountingMsg => IsPtBr ? "Desmontando install.wim e salvando as alterações…" : "Unmounting install.wim and saving changes…";
    public string Stage5Bypasses => IsPtBr ? "Ajustando os requisitos de instalação" : "Adjusting setup requirements";
    public string Stage5BypassesMsg => IsPtBr ? "Configurando boot.wim e autounattend.xml…" : "Configuring boot.wim and autounattend.xml…";
    public string Stage6FinalizingIso => IsPtBr ? "Gerando a ISO" : "Building the ISO";
    public string Stage6WritingUsb => IsPtBr ? "Gravando o pendrive" : "Writing the USB drive";
    public string StageCleanup => IsPtBr ? "Limpando arquivos temporários…" : "Cleaning up temporary files…";

    // Dialogs
    public string AdminRequiredTitle => IsPtBr ? "Permissão de administrador" : "Administrator permission";
    public string AdminRequiredPrompt => IsPtBr
        ? "O DISM precisa de privilégios de administrador. Reiniciar o Kiso11 como administrador?"
        : "DISM needs administrator rights. Restart Kiso11 as administrator?";
    public string Restart => IsPtBr ? "Reiniciar" : "Restart";

    public string UsbFormatConfirmTitle => IsPtBr ? "Formatar o pendrive?" : "Format the USB drive?";
    public string UsbFormatConfirm(string letter, string label, string size) => IsPtBr
        ? $"A unidade {letter} ({label}, {size}) será formatada. Todos os dados serão apagados."
        : $"Drive {letter} ({label}, {size}) will be formatted. All data will be erased.";
    public string FormatAndWrite => IsPtBr ? "Formatar e gravar" : "Format and write";

    public string SuccessTitle => IsPtBr ? "Concluído" : "Done";
    public string SuccessIsoCreated(string path) => IsPtBr
        ? $"ISO criada em:\n{path}"
        : $"ISO saved to:\n{path}";
    public string SuccessUsbCreated(string letter) => IsPtBr
        ? $"Pendrive bootável pronto na unidade {letter}."
        : $"Bootable USB drive ready on drive {letter}.";
    public string ShowInExplorer => IsPtBr ? "Mostrar no Explorer" : "Show in Explorer";

    public string CancelOperationTitle => IsPtBr ? "Cancelar a operação?" : "Cancel the operation?";
    public string CancelOperationMsg => IsPtBr
        ? "As imagens montadas serão descartadas com segurança."
        : "Mounted images will be discarded safely.";
    public string CancelOperationConfirm => IsPtBr ? "Cancelar operação" : "Cancel operation";
    public string KeepRunning => IsPtBr ? "Continuar" : "Keep running";

    public string ClosePromptRunningTitle => IsPtBr ? "Operação em andamento" : "Operation in progress";
    public string ClosePromptRunningMsg => IsPtBr
        ? "Minimizar mantém o processo na bandeja. Fechar cancela a operação."
        : "Minimizing keeps the process in the tray. Closing cancels it.";
    public string MinimizeAction => IsPtBr ? "Minimizar" : "Minimize";
    public string CancelAndClose => IsPtBr ? "Cancelar e fechar" : "Cancel and close";

    public string ErrorTitle => IsPtBr ? "A operação falhou" : "Operation failed";
    public string ErrorBody(string message) => IsPtBr
        ? $"{message}\n\nVeja o log para mais detalhes."
        : $"{message}\n\nSee the log for details.";

    // System tray
    public string TrayOpen => IsPtBr ? "Abrir Kiso11" : "Open Kiso11";
    public string TrayStatus => IsPtBr ? "Status: " : "Status: ";
    public string TrayLanguage => IsPtBr ? "Idioma" : "Language";
    public string TrayExit => IsPtBr ? "Sair" : "Exit";
    public string TrayMinimizedTitle => IsPtBr ? "Kiso11 minimizado" : "Kiso11 minimized";
    public string TrayMinimizedMsg => IsPtBr ? "Continua na bandeja do sistema." : "Still running in the system tray.";
    public string TrayCompletedTitle => IsPtBr ? "Kiso11 concluído" : "Kiso11 finished";
    public string TrayCompletedMsg => IsPtBr ? "A operação terminou com sucesso." : "The operation finished successfully.";

    // Errors and messages from services
    public string ErrGeneric => IsPtBr ? "Erro: {0}" : "Error: {0}";
    public string StatusCancelling => IsPtBr ? "Cancelando… aguarde a limpeza." : "Cancelling… waiting for cleanup.";
    public string LogCancelledByUser => IsPtBr ? "Operação cancelada pelo usuário." : "Operation cancelled by user.";
    public string LogProcessFailed => IsPtBr ? "Falha ao executar {0}: {1}" : "Failed to run {0}: {1}";
    public string ErrInvalidDrive => IsPtBr ? "Letra de unidade inválida." : "Invalid drive letter.";
    public string ErrSystemDrive => IsPtBr ? "Operação bloqueada: esta é a unidade do sistema." : "Blocked: this is the system drive.";
    public string ErrNotRemovable => IsPtBr ? "A unidade {0} não é removível. Nada foi formatado." : "Drive {0} is not removable. Nothing was formatted.";
    public string ErrUsbTooLarge => IsPtBr ? "A unidade {0} tem mais de 32 GB. O Windows não formata FAT32 nesse tamanho." : "Drive {0} is over 32 GB. Windows cannot format FAT32 at that size.";
    public string ErrFormatUsb => IsPtBr ? "Não foi possível formatar a unidade {0}. Verifique se ela está em uso." : "Could not format drive {0}. Check whether it is in use.";
    public string ErrRobocopy => IsPtBr ? "Robocopy falhou ao copiar arquivos (código {0})." : "Robocopy failed to copy files (exit code {0}).";
    public string ErrInstallWimMissing => IsPtBr ? "install.wim não encontrado no diretório de origem." : "install.wim was not found in the source folder.";
    public string ErrSplitWim => IsPtBr ? "Falha ao dividir install.wim com DISM (código {0})." : "DISM failed to split install.wim (exit code {0}).";
    public string MsgFormattingUsb => IsPtBr ? "Formatando o pendrive em FAT32…" : "Formatting the USB drive as FAT32…";
    public string MsgCopyingBootFiles => IsPtBr ? "Copiando arquivos de inicialização…" : "Copying boot files…";
    public string MsgSplittingWim => IsPtBr ? "install.wim passa de 4 GB. Dividindo em partes .swm…" : "install.wim is over 4 GB. Splitting into .swm parts…";
    public string MsgCopyingWim => IsPtBr ? "Copiando install.wim…" : "Copying install.wim…";
    public string MsgWritingBootSector => IsPtBr ? "Gravando o setor de inicialização…" : "Writing the boot sector…";
    public string MsgUsbReady => IsPtBr ? "Pendrive pronto." : "USB drive ready.";

    public string ErrReadEditions => IsPtBr ? "Não foi possível ler as edições do Windows nesta imagem." : "Could not read the Windows editions in this image.";
    public string ErrMountWim => IsPtBr ? "Falha ao montar a imagem em {0} (código {1})." : "Could not mount the image at {0} (exit code {1}).";
    public string ErrUnmountWim => IsPtBr ? "Falha ao salvar e desmontar {0} (código {1})." : "Could not save and unmount {0} (exit code {1}).";
    public string ErrExportWim => IsPtBr ? "Falha ao exportar a imagem otimizada (código {0})." : "Could not export the optimized image (exit code {0}).";
    public string ErrLoadHive => IsPtBr ? "Não foi possível carregar o registro de {0}. Feche programas que possam estar usando o arquivo e tente de novo." : "Could not load the registry hive {0}. Close programs that may be using it and try again.";
    public string MsgRemovingApps => IsPtBr ? "Removendo apps ({0}/{1})" : "Removing apps ({0}/{1})";
    public string MsgRemovingAi => IsPtBr ? "Removendo IA e Copilot: {0}" : "Removing AI and Copilot: {0}";
    public string LogFileDeleteFailed => IsPtBr ? "Não foi possível apagar {0}: {1}" : "Could not delete {0}: {1}";
    public string LogEdgeRemoveFailed => IsPtBr ? "A remoção do Edge retornou o código {0}." : "Edge removal returned exit code {0}.";
    public string LogRegistryValueFailed => IsPtBr ? "Falha ao gravar {0} em {1}." : "Could not write {0} to {1}.";
    public string LogRegistryFailures => IsPtBr ? "{0} ajuste(s) de registro falharam. Veja o log acima." : "{0} registry setting(s) failed. See the log above.";
    public string LogHiveUnloadFailed => IsPtBr ? "Não foi possível descarregar o registro {0}. Reinicie antes de montar de novo." : "Could not unload the registry hive {0}. Restart before mounting again.";
    public string LogBootWimSkipped => IsPtBr ? "Bypass do boot.wim ignorado: {0}" : "Skipped the boot.wim bypass: {0}";
    public string LogDriversFailed => IsPtBr ? "A integração de drivers retornou o código {0}." : "Driver integration returned exit code {0}.";
}
