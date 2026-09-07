using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using Kiso11.ViewModels;

namespace Kiso11.Services;

public class TrayService : IDisposable
{
    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;

    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;
    private const int NIF_INFO = 0x00000010;

    private const int NIIF_NONE = 0x00000000;
    private const int NIIF_INFO = 0x00000001;
    private const int NIIF_WARNING = 0x00000002;
    private const int NIIF_ERROR = 0x00000003;

    private const int WM_USER = 0x0400;
    public const int WM_TRAY_CALLBACK = WM_USER + 2048;

    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private readonly Window _window;
    private readonly MainViewModel _vm;
    private IntPtr _hwnd;
    private HwndSource? _hwndSource;
    private IntPtr _hIcon = IntPtr.Zero;
    private bool _isIconAdded;
    private ContextMenu? _currentMenu;

    public TrayService(Window window, MainViewModel vm)
    {
        _window = window;
        _vm = vm;

        _window.SourceInitialized += OnSourceInitialized;
        _window.StateChanged += OnWindowStateChanged;

        _vm.RequestMinimizeToTray += MinimizeToTray;
        _vm.TrayStatusUpdated += UpdateTooltip;
        _vm.TrayCompleted += () => ShowBalloon(_vm.Loc.TrayCompletedTitle, _vm.Loc.TrayCompletedMsg);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(_window).Handle;
        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(WndProc);

        LoadIcon();
        AddTrayIcon();
    }

    private void LoadIcon()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(baseDir, "Assets", "kiso11.ico");
            if (File.Exists(path))
            {
                _hIcon = LoadImage(IntPtr.Zero, path, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
            }

            if (_hIcon == IntPtr.Zero)
            {
                var exePath = Environment.ProcessPath ?? "";
                if (File.Exists(exePath))
                {
                    _hIcon = ExtractIcon(IntPtr.Zero, exePath, 0);
                }
            }
        }
        catch { }
    }

    private void AddTrayIcon()
    {
        if (_hwnd == IntPtr.Zero || _hIcon == IntPtr.Zero) return;

        var data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAY_CALLBACK,
            hIcon = _hIcon,
            szTip = "Kiso11 - Windows 11 Debloater"
        };

        _isIconAdded = Shell_NotifyIcon(NIM_ADD, ref data);
    }

    public void UpdateTooltip(string tip)
    {
        if (!_isIconAdded || _hwnd == IntPtr.Zero) return;

        if (tip.Length > 127) tip = tip.Substring(0, 124) + "...";

        var data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_TIP,
            szTip = tip
        };

        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    public void ShowBalloon(string title, string message, bool isError = false)
    {
        if (!_isIconAdded || _hwnd == IntPtr.Zero) return;

        if (title.Length > 63) title = title.Substring(0, 60) + "...";
        if (message.Length > 255) message = message.Substring(0, 252) + "...";

        var data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_INFO,
            szInfoTitle = title,
            szInfo = message,
            dwInfoFlags = isError ? NIIF_ERROR : NIIF_INFO
        };

        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    public void MinimizeToTray()
    {
        _window.Hide();
        ShowBalloon(_vm.Loc.TrayMinimizedTitle, _vm.Loc.TrayMinimizedMsg);
    }

    public void RestoreFromTray()
    {
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
        SetForegroundWindow(_hwnd);
        _window.Focus();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (_window.WindowState == WindowState.Minimized)
        {
            MinimizeToTray();
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAY_CALLBACK)
        {
            int action = lParam.ToInt32();
            if (action == WM_LBUTTONUP || action == WM_LBUTTONDBLCLK)
            {
                if (!_window.IsVisible || _window.WindowState == WindowState.Minimized)
                {
                    RestoreFromTray();
                }
                else
                {
                    _window.Activate();
                    SetForegroundWindow(_hwnd);
                }
                handled = true;
            }
            else if (action == WM_RBUTTONUP)
            {
                ShowContextMenu();
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    private void ShowContextMenu()
    {
        if (_currentMenu != null && _currentMenu.IsOpen)
        {
            _currentMenu.IsOpen = false;
        }

        var loc = _vm.Loc;
        var menu = new ContextMenu();

        // 1. Abrir Kiso11
        var itemOpen = new MenuItem
        {
            Header = loc.TrayOpen,
            FontWeight = FontWeights.Bold
        };
        itemOpen.Click += (s, e) => RestoreFromTray();
        menu.Items.Add(itemOpen);

        // 2. Minimizar para bandeja
        var itemMin = new MenuItem
        {
            Header = loc.TrayMinimize,
            IsEnabled = _window.IsVisible && _window.WindowState != WindowState.Minimized
        };
        itemMin.Click += (s, e) => MinimizeToTray();
        menu.Items.Add(itemMin);

        menu.Items.Add(new Separator());

        // 3. Status atual
        var itemStatus = new MenuItem
        {
            Header = $"{loc.TrayStatus}{_vm.StateText} ({_vm.ProgressPercentage}%)",
            IsEnabled = false
        };
        menu.Items.Add(itemStatus);

        menu.Items.Add(new Separator());

        // 4. Idioma Submenu
        var itemLang = new MenuItem { Header = loc.TrayLanguage };

        var itemPt = new MenuItem
        {
            Header = "Português (PT-BR)",
            IsChecked = loc.IsPtBr
        };
        itemPt.Click += (s, e) => _vm.SetLanguage(AppLanguage.PtBr);
        itemLang.Items.Add(itemPt);

        var itemEn = new MenuItem
        {
            Header = "English (EN)",
            IsChecked = loc.IsEn
        };
        itemEn.Click += (s, e) => _vm.SetLanguage(AppLanguage.En);
        itemLang.Items.Add(itemEn);

        menu.Items.Add(itemLang);

        menu.Items.Add(new Separator());

        // 5. Sair
        var itemExit = new MenuItem
        {
            Header = loc.TrayExit
        };
        itemExit.Click += (s, e) =>
        {
            Dispose();
            System.Windows.Application.Current.Shutdown();
        };
        menu.Items.Add(itemExit);

        _currentMenu = menu;

        // Position menu at cursor
        GetCursorPos(out var pt);
        SetForegroundWindow(_hwnd);

        menu.Placement = PlacementMode.AbsolutePoint;
        menu.HorizontalOffset = pt.X;
        menu.VerticalOffset = pt.Y;
        menu.IsOpen = true;
    }

    public void Dispose()
    {
        if (_isIconAdded && _hwnd != IntPtr.Zero)
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1
            };
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _isIconAdded = false;
        }

        _hwndSource?.RemoveHook(WndProc);

        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
    }
}
