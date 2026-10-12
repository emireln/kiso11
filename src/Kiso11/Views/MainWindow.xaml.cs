using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Kiso11.Services;
using Kiso11.ViewModels;

namespace Kiso11.Views;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35; // Windows 11 only; ignored on Windows 10
    private const int DWMWA_TEXT_COLOR = 36;    // Windows 11 only; ignored on Windows 10

    private TrayService? _trayService;
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();

        if (DataContext is MainViewModel vm)
        {
            _trayService = new TrayService(this, vm);
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
        Closed += (_, _) => _trayService?.Dispose();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Match the title bar to the app surface so the window reads as one piece.
        var handle = new WindowInteropHelper(this).Handle;
        SetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, 1);
        SetWindowAttribute(handle, DWMWA_CAPTION_COLOR, ColorRef(0x10, 0x10, 0x12));
        SetWindowAttribute(handle, DWMWA_TEXT_COLOR, ColorRef(0xF4, 0xF4, 0xF5));
    }

    private static void SetWindowAttribute(IntPtr handle, int attribute, int value)
    {
        try
        {
            DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
        }
        catch { }
    }

    /// COLORREF is 0x00BBGGRR.
    private static int ColorRef(int r, int g, int b) => (b << 16) | (g << 8) | r;

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Logs))
        {
            // Wait for layout so the new line is measured before scrolling.
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () => LogScroller.ScrollToEnd());
        }
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_closeConfirmed || DataContext is not MainViewModel vm || !vm.IsProcessing)
        {
            _trayService?.Dispose();
            return;
        }

        e.Cancel = true;

        var result = MessageBox.Show(
            vm.Loc.ClosePromptRunningMsg,
            vm.Loc.ClosePromptRunningTitle,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _trayService?.MinimizeToTray();
            return;
        }

        if (result == MessageBoxResult.Cancel)
        {
            return;
        }

        // Wait for the cleanup so no WIM is left mounted after the app exits.
        await vm.CancelAndWaitAsync();
        _closeConfirmed = true;
        Close();
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
}
