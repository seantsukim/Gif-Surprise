using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

// Minimal random-video desktop popup with green-screen removal.
// Put your green-screen .mp4 files in a "videos" folder next to the .csproj.
// Shows a small always-on-top, borderless window that plays a random video
// with sound, keying out the green background so only the subject shows.
// It never steals keyboard/mouse focus, so it won't interrupt your workflow.
// Right-click the tray icon and choose Exit to close it.

class VideoPopup : Window
{
    const int W = 360, H = 240;

    // The real video plays here, off-screen, never seen directly.
    readonly Window hiddenHost;
    readonly MediaElement player = new MediaElement();

    // The visible window shows only the chroma-keyed result.
    readonly System.Windows.Controls.Image displayImage = new System.Windows.Controls.Image();
    readonly WriteableBitmap bitmap = new WriteableBitmap(W, H, 96, 96, PixelFormats.Pbgra32, null);
    byte[] pixelBuffer;

    readonly Random rng = new Random();
    readonly string[] videos;
    readonly Forms.NotifyIcon trayIcon = new Forms.NotifyIcon();
    int frameSkip = 0;

    public VideoPopup(string folder)
    {
        videos = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.mp4") : Array.Empty<string>();
        pixelBuffer = new byte[W * H * 4];

        // --- Visible window: shows only the keyed-out result ---
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Width = W;
        Height = H;
        WindowStartupLocation = WindowStartupLocation.Manual;
        displayImage.Source = bitmap;
        displayImage.Stretch = Stretch.Uniform;
        Content = displayImage;

        // --- Hidden window: hosts the real MediaElement, off virtual screen ---
        hiddenHost = new Window
        {
            Width = W,
            Height = H,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Left = -100000,
            Top = -100000,
            Content = player
        };
        player.LoadedBehavior = MediaState.Manual;
        player.UnloadedBehavior = MediaState.Manual;
        player.Stretch = Stretch.Uniform;
        player.Volume = 0.7;
        player.MediaEnded += (s, e) => ShowRandomVideo();
        hiddenHost.Show();

        // Tray icon so there's a reliable way to close a window that never takes focus.
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Exit", null, (s, e) => Application.Current.Shutdown());
        trayIcon.Icon = System.Drawing.SystemIcons.Application;
        trayIcon.Text = "Video Popup (right-click to exit)";
        trayIcon.ContextMenuStrip = menu;
        trayIcon.Visible = true;
        Closed += (s, e) => { trayIcon.Visible = false; hiddenHost.Close(); };

        CompositionTarget.Rendering += OnRendering;
        Closed += (s, e) => CompositionTarget.Rendering -= OnRendering;

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
        MakeNoActivate(this);
    }

    void MakeNoActivate(Window w)
    {
        var hwnd = new WindowInteropHelper(w).Handle;
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

    // Captures the hidden video frame, keys out green pixels, and pushes
    // the result into the visible bitmap. Skips every other tick to save CPU.
    void OnRendering(object sender, EventArgs e)
    {
        if (player.Source == null || player.NaturalVideoWidth == 0) return;
        if (++frameSkip % 2 != 0) return;

        var rtb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(player);
        rtb.CopyPixels(pixelBuffer, W * 4, 0);

        for (int i = 0; i < pixelBuffer.Length; i += 4)
        {
            byte b = pixelBuffer[i];
            byte g = pixelBuffer[i + 1];
            byte r = pixelBuffer[i + 2];

            // Green screen test: green channel clearly dominant over red & blue.
            if (g > 60 && g > r * 1.3 && g > b * 1.3)
            {
                pixelBuffer[i] = 0;
                pixelBuffer[i + 1] = 0;
                pixelBuffer[i + 2] = 0;
                pixelBuffer[i + 3] = 0; // fully transparent
            }
        }

        bitmap.WritePixels(new Int32Rect(0, 0, W, H), pixelBuffer, W * 4, 0);
    }

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
