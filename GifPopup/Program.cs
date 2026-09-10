using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

// Minimal random-video desktop popup.
// Put your .mp4 files in a "videos" folder next to the .csproj (project root).
// Shows a small always-on-top, borderless window that plays a random video
// with sound, then moves on to another random one when it finishes.
// It never steals keyboard/mouse focus, so it won't interrupt your workflow.
// Right-click the tray icon and choose Exit to close it.

class VideoPopup : Window
{
    readonly MediaElement player = new MediaElement();
    readonly Random rng = new Random();
    readonly string[] videos;
    readonly Forms.NotifyIcon trayIcon = new Forms.NotifyIcon();

    public VideoPopup(string folder)
    {
        videos = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.mp4") : Array.Empty<string>();

        // Tray icon so there's a reliable way to close a window that never takes focus.
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Exit", null, (s, e) => Application.Current.Shutdown());
        trayIcon.Icon = System.Drawing.SystemIcons.Application;
        trayIcon.Text = "Video Popup (right-click to exit)";
        trayIcon.ContextMenuStrip = menu;
        trayIcon.Visible = true;
        Closed += (s, e) => trayIcon.Visible = false;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false; // don't take focus when first shown
        Width = 360;
        Height = 240;
        WindowStartupLocation = WindowStartupLocation.Manual;

        player.LoadedBehavior = MediaState.Manual;
        player.UnloadedBehavior = MediaState.Manual;
        player.Stretch = Stretch.Uniform; // keep aspect ratio, letterbox if needed
        player.Volume = 0.7;
        player.MediaEnded += (s, e) => ShowRandomVideo();
        Content = player;

        ShowRandomVideo();
    }

    // --- Prevents the window from ever taking keyboard/mouse focus ---
    const int GWL_EXSTYLE = -20;
    const int WS_EX_NOACTIVATE = 0x08000000;
    const int WM_MOUSEACTIVATE = 0x0021;
    const int MA_NOACTIVATE = 3;

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);
        HwndSource.FromHwnd(hwnd).AddHook(WndProc);
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_MOUSEACTIVATE)
        {
            handled = true;
            return (IntPtr)MA_NOACTIVATE;
        }
        return IntPtr.Zero;
    }
    // -------------------------------------------------------------

    void ShowRandomVideo()
    {
        if (videos.Length == 0) return;

        var path = videos[rng.Next(videos.Length)];
        player.Source = new Uri(path);
        player.Play();

        var area = SystemParameters.WorkArea;
        Left = rng.Next(0, Math.Max(1, (int)(area.Width - Width)));
        Top = rng.Next(0, Math.Max(1, (int)(area.Height - Height)));

        if (!IsVisible) Show();
    }

    [STAThread]
    static void Main()
    {
        // "videos" folder lives next to the .csproj (the project root), not in
        // bin\Debug\... so it survives rebuilds and `dotnet clean`.
        string folder = Path.Combine(FindProjectRoot(), "videos");

        var app = new Application();
        app.Run(new VideoPopup(folder));
    }

    // Walks up from the .exe's folder (e.g. bin\Debug\net8.0-windows\) until
    // it finds a folder containing a .csproj file.
    static string FindProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (dir.GetFiles("*.csproj").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }
        return AppContext.BaseDirectory;
    }
}
