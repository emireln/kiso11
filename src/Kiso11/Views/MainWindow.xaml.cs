using System;
using System.Windows;
using System.Windows.Controls;
using Kiso11.Services;
using Kiso11.ViewModels;

namespace Kiso11.Views;

public partial class MainWindow : Window
{
    private TrayService? _trayService;

    public MainWindow()
    {
        InitializeComponent();

        if (DataContext is MainViewModel vm)
        {
            _trayService = new TrayService(this, vm);
        }

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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Activate();
        Focus();
    }

    private void MinimizeWindow(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void ToggleMaximizeWindow(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseWindow(object sender, RoutedEventArgs e) => Close();

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

    private async void OnWindowDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files != null && files.Length > 0 && files[0].EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.IsoPath = files[0];
                    await vm.InspectSourceAsync();
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
