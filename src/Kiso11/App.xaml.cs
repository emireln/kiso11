using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kiso11.Models;
using Kiso11.Services;
using Kiso11.ViewModels;

namespace Kiso11;

public partial class App : System.Windows.Application
{
    private const double PreviewWidth = 880;
    private const double PreviewHeight = 640;

    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Kiso11",
        "logs");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        DispatcherUnhandledException += (_, args) =>
        {
            WriteLog($"UI exception: {args.Exception}");
            var loc = Services.LocalizationService.Instance;
            Services.Dialogs.Info(loc.ErrorTitle, loc.ErrorBody(args.Exception.Message), loc.Ok);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) => WriteLog($"Unhandled exception: {args.ExceptionObject}");

        if (e.Args.Length > 0 && e.Args[0] == "--render-preview")
        {
            RenderPreview(e.Args.Length > 1 ? e.Args[1] : AppDomain.CurrentDomain.BaseDirectory);
            Shutdown(0);
            return;
        }

        try
        {
            var mainWindow = new Views.MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            WriteLog($"Startup failed: {ex}");
            var loc = Services.LocalizationService.Instance;
            Services.Dialogs.Info(loc.ErrorTitle, loc.ErrorBody(ex.Message), loc.Ok);
            Shutdown(1);
        }
    }

    private static void WriteLog(string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(
                Path.Combine(LogDirectory, "kiso11.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    /// Renders each screen to a PNG so the layout can be checked without running a real operation.
    private static void RenderPreview(string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var loc = LocalizationService.Instance;
        var editions = new[] { new WimEdition(1, "Windows 11 Home"), new WimEdition(6, "Windows 11 Pro") };
        const string isoPath = @"C:\ISO\Win11_24H2_BrazilianPortuguese_x64.iso";

        void Shot(string name, int step, Action<MainViewModel> setup)
        {
            var window = new Views.MainWindow();
            var vm = (MainViewModel)window.DataContext;
            setup(vm);
            vm.CurrentStep = step;
            SavePng(window, Path.Combine(outputDir, name + ".png"));
        }

        void Languages(string suffix, AppLanguage language)
        {
            loc.CurrentLanguage = language;

            Shot($"step1_empty{suffix}", 1, _ => { });

            Shot($"step1_loaded{suffix}", 1, vm =>
            {
                vm.ApplyPreview(isoPath, "Win11_24H2_BrazilianPortuguese_x64.iso", "5,42 GB", editions);
            });

            Shot($"step2_iso{suffix}", 2, vm =>
            {
                vm.ApplyPreview(isoPath, "Win11_24H2_BrazilianPortuguese_x64.iso", "5,42 GB", editions);
                vm.OutputMode = OutputMode.IsoFile;
            });

            Shot($"step2_usb{suffix}", 2, vm =>
            {
                vm.ApplyPreview(isoPath, "Win11_24H2_BrazilianPortuguese_x64.iso", "5,42 GB", editions);
                vm.UsbDrives.Clear();
                vm.UsbDrives.Add(new UsbDriveItem { DriveLetter = "E:", VolumeLabel = "KINGSTON", TotalBytes = 15_000_000_000, FreeBytes = 14_000_000_000 });
                vm.UsbDrives.Add(new UsbDriveItem { DriveLetter = "F:", VolumeLabel = "BACKUP", TotalBytes = 64_000_000_000, FreeBytes = 60_000_000_000 });
                vm.SelectedUsbDrive = vm.UsbDrives[0];
                vm.OutputMode = OutputMode.BootableUsb;
            });

            Shot($"step3_options{suffix}", 3, vm =>
            {
                vm.ApplyPreview(isoPath, "Win11_24H2_BrazilianPortuguese_x64.iso", "5,42 GB", editions);
                vm.OutputMode = OutputMode.IsoFile;
            });

            Shot($"step4_running{suffix}", 4, vm =>
            {
                vm.ApplyPreview(isoPath, "Win11_24H2_BrazilianPortuguese_x64.iso", "5,42 GB", editions);
                vm.State = ProcessState.Running;
                vm.CurrentStageText = loc.Stage3Debloating;
                vm.ProgressPercentage = 40;
                vm.StatusMessage = string.Format(loc.MsgRemovingApps, 5, 12);
                vm.ElapsedTimeFormatted = "02:14";
                vm.EstimatedRemainingFormatted = "~01:05";
                vm.LogText =
                    "[10:02:11] Edição: Windows 11 Pro\n" +
                    "[10:02:12] Montando a ISO…\n" +
                    "[10:02:19] Copiando arquivos da ISO…\n" +
                    "[10:03:40] Montando install.wim\n" +
                    "[10:03:52] Removendo apps (5/12)";
            });

            Shot($"step4_done{suffix}", 4, vm =>
            {
                vm.ApplyPreview(isoPath, "Win11_24H2_BrazilianPortuguese_x64.iso", "5,42 GB", editions);
                vm.State = ProcessState.Completed;
                vm.CurrentStageText = loc.StateCompleted;
                vm.ProgressPercentage = 100;
                vm.StatusMessage = loc.ProcessFinishedSuccess;
                vm.ElapsedTimeFormatted = "08:41";
                vm.EstimatedRemainingFormatted = "--:--";
            });
        }

        Languages("_pt", AppLanguage.PtBr);
        Languages("_en", AppLanguage.En);

        var dialog = new Views.ThemedDialog(
            loc.UsbFormatConfirmTitle,
            loc.UsbFormatConfirm("E:", "KINGSTON", "14,9 GB"),
            new[]
            {
                new DialogButton(loc.Cancel, DialogButtonKind.Secondary),
                new DialogButton(loc.FormatAndWrite, DialogButtonKind.Danger)
            });
        SaveElement((System.Windows.FrameworkElement)dialog.Content, Path.Combine(outputDir, "dialog_format_pt.png"), dialog.Width);

        loc.CurrentLanguage = AppLanguage.PtBr;
    }

    /// Renders a dialog at its natural height.
    private static void SaveElement(System.Windows.FrameworkElement root, string path, double width)
    {
        root.Measure(new System.Windows.Size(width, double.PositiveInfinity));
        var height = root.DesiredSize.Height;
        root.Arrange(new System.Windows.Rect(0, 0, width, height));
        root.UpdateLayout();

        var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void SavePng(Views.MainWindow window, string path)
    {
        window.Width = PreviewWidth;
        window.Height = PreviewHeight;

        var root = (System.Windows.FrameworkElement)window.Content;
        root.Width = PreviewWidth;
        root.Height = PreviewHeight;
        root.Measure(new System.Windows.Size(PreviewWidth, PreviewHeight));
        root.Arrange(new System.Windows.Rect(0, 0, PreviewWidth, PreviewHeight));
        root.UpdateLayout();

        // Command enabled-state is refreshed by a deferred requery, so let it run before capturing.
        CommandManager.InvalidateRequerySuggested();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        root.UpdateLayout();

        var bitmap = new RenderTargetBitmap((int)PreviewWidth, (int)PreviewHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
