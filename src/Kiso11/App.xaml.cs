using System;
using System.IO;
using System.Windows;

namespace Kiso11;

public partial class App : System.Windows.Application
{
    private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_log.txt");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        try
        {
            File.WriteAllText(LogFile, $"[{DateTime.Now}] App.OnStartup starting...\n");
        }
        catch { }

        Exit += (s, ev) =>
        {
            try { File.AppendAllText(LogFile, $"Application Exit event fired! ExitCode: {ev.ApplicationExitCode}\n"); } catch { }
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var msg = $"AppDomain Unhandled: {args.ExceptionObject}\n";
            try { File.AppendAllText(LogFile, msg); } catch { }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            var msg = $"Dispatcher Unhandled: {args.Exception.Message}\n{args.Exception.StackTrace}\nInner: {args.Exception.InnerException?.Message}\n";
            try { File.AppendAllText(LogFile, msg); } catch { }
            args.Handled = true;
        };

        if (e.Args.Length > 0 && e.Args[0] == "--render-preview")
        {
            var outputDir = e.Args.Length > 1 ? e.Args[1] : AppDomain.CurrentDomain.BaseDirectory;
            Directory.CreateDirectory(outputDir);

            var win = new Views.MainWindow();
            var vm = win.DataContext as ViewModels.MainViewModel;
            win.Width = 820;
            win.Height = 620;

            void SaveStep(int step, string filename)
            {
                var win = new Views.MainWindow();
                var vm = win.DataContext as ViewModels.MainViewModel;
                if (vm != null)
                {
                    vm.CurrentStep = step;
                    if (filename.Contains("loaded"))
                    {
                        var dummyIso = Path.Combine(outputDir, "Win11_24H2_BrazilianPortuguese_x64.iso");
                        if (!File.Exists(dummyIso)) File.WriteAllBytes(dummyIso, new byte[1024]);
                        vm.IsoPath = dummyIso;
                        vm.IsoFileName = "Win11_24H2_BrazilianPortuguese_x64.iso";
                        vm.IsoSizeText = "5.42 GB";
                    }
                    else if (step > 1)
                    {
                        vm.IsIsoValid = true;
                        vm.IsoPath = @"C:\ISO\Win11_24H2_BrazilianPortuguese_x64.iso";
                    }
                    if (step == 2)
                    {
                        vm.OutputMode = Models.OutputMode.BootableUsb;
                    }
                    if (step == 4)
                    {
                        vm.ProgressPercentage = 68;
                        vm.CurrentStageText = "Otimizando componentes e removendo bloatware...";
                        vm.StatusMessage = "Processando pacotes AppX no WIM (68%)...";
                        vm.ElapsedTimeFormatted = "02:14";
                        vm.EstimatedRemainingFormatted = "01:05";
                        vm.LogText = "[Kiso11] Iniciando otimização da ISO: Win11_24H2_BrazilianPortuguese_x64.iso\n[Kiso11] Extraindo arquivos da imagem...\n[Kiso11] Montando install.wim...\n[DISM] Removendo pacotes AppX inúteis (BingNews, Clipchamp, Xbox, etc.)...\n[DISM] Removendo IA, Copilot e Recall provisionados...\n[DISM] Configurando Bypass de Requisitos de Hardware (LabConfig TPM/SecureBoot)...\n[DISM] Habilitando BypassNRO (Conta Local sem internet)...";
                    }
                }
                var visual = win.Content as FrameworkElement;
                if (visual != null)
                {
                    visual.Width = 820;
                    visual.Height = 620;
                    visual.Measure(new Size(820, 620));
                    visual.Arrange(new Rect(0, 0, 820, 620));
                    visual.UpdateLayout();

                    var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(820, 620, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    rtb.Render(visual);

                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                    using var fs = File.Create(Path.Combine(outputDir, filename));
                    encoder.Save(fs);
                }
            }

            SaveStep(1, "step1_iso.png");
            SaveStep(1, "step1_iso_loaded.png");
            SaveStep(2, "step2_target.png");
            Services.LocalizationService.Instance.CurrentLanguage = Services.AppLanguage.PtBr;
            SaveStep(3, "step3_tweaks_pt.png");
            Services.LocalizationService.Instance.CurrentLanguage = Services.AppLanguage.En;
            SaveStep(3, "step3_tweaks_en.png");
            SaveStep(4, "step4_progress.png");

            Shutdown(0);
            return;
        }

        try
        {
            File.AppendAllText(LogFile, "App: creating MainWindow...\n");
            var mainWindow = new Views.MainWindow();
            MainWindow = mainWindow;

            File.AppendAllText(LogFile, "App: calling mainWindow.Show()...\n");
            mainWindow.Show();
            File.AppendAllText(LogFile, "App: mainWindow.Show() returned.\n");
        }
        catch (Exception ex)
        {
            var msg = $"App: MainWindow startup exception: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException?.Message}\n";
            try { File.AppendAllText(LogFile, msg); } catch { }
            MessageBox.Show(msg, "Kiso11 Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
