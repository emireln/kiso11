using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Kiso11.Services;
using Kiso11.ViewModels;

namespace Kiso11.Views;

public partial class MainWindow : Window
{
    private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_log.txt");

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool SetWindowText(IntPtr hWnd, string lpString);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int SW_SHOWNORMAL = 1;

    private TrayService? _trayService;

    public MainWindow()
    {
        InitializeComponent();

        if (DataContext is MainViewModel vm)
        {
            _trayService = new TrayService(this, vm);
        }

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += (s, e) => _trayService?.Dispose();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.IsProcessing)
        {
            var result = MessageBox.Show(
                vm.Loc.ClosePromptRunningMsg,
                vm.Loc.ClosePromptRunningTitle,
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                e.Cancel = true;
                _trayService?.MinimizeToTray();
                return;
            }
            else if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            vm.CancelProcessCommand.Execute(null);
        }

        _trayService?.Dispose();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            int useDarkMode = 1;
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
        }
        catch { }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (!string.IsNullOrEmpty(Title))
            {
                SetWindowText(handle, Title);
            }
            ShowWindow(handle, SW_SHOWNORMAL);
            BringWindowToTop(handle);
            SetForegroundWindow(handle);

            Topmost = true;
            Topmost = false;
            Activate();
            Focus();

            File.AppendAllText(LogFile, $"OnLoaded: Handle={handle}, IsVisible={IsVisible}, Win32Visible={IsWindowVisible(handle)}, Title='{Title}', Bounds=[{Left},{Top},{Width},{Height}]\n");
        }
        catch (Exception ex)
        {
            try { File.AppendAllText(LogFile, $"OnLoaded ex: {ex}\n"); } catch { }
        }
    }

    private void OnWindowDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files != null && files.Length > 0 && files[0].EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
                e.Handled = true;
                return;
            }
        }
        e.Effects = System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void OnWindowDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files != null && files.Length > 0 && files[0].EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.IsoPath = files[0];
                }
            }
        }
    }

    private void OnLogTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.CaretIndex = textBox.Text.Length;
            textBox.ScrollToEnd();
        }
    }
}
