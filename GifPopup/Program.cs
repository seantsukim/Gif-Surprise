using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

// Minimal random-GIF desktop popup.
// Put your .gif files in a "gifs" folder next to the .exe.
// It shows a small always-on-top window that never steals focus, so it won't
// interrupt whatever you're typing or clicking in another app.
// Right-click the tray icon and choose Exit to close it.

class GifPopup : Form
{
    readonly PictureBox pb = new PictureBox();
    readonly Random rng = new Random();
    readonly string[] gifs;
    readonly Timer switchTimer = new Timer();
    readonly NotifyIcon trayIcon = new NotifyIcon();

    public GifPopup(string folder)
    {
        gifs = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.gif") : Array.Empty<string>();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Exit", null, (s, e) => Application.Exit());
        trayIcon.Icon = SystemIcons.Application;
        trayIcon.Text = "Gif Popup (right-click to exit)";
        trayIcon.ContextMenuStrip = menu;
        trayIcon.Visible = true;

        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(220, 220);
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta; // makes the background transparent

        pb.Dock = DockStyle.Fill;
        pb.SizeMode = PictureBoxSizeMode.Zoom;
        pb.BackColor = Color.Transparent;
        Controls.Add(pb);

        switchTimer.Interval = 20000; // new random gif every 20s
        switchTimer.Tick += (s, e) => ShowRandomGif();
        switchTimer.Start();

        ShowRandomGif();

        FormClosed += (s, e) => trayIcon.Visible = false;
    }

    // Prevents the window from ever taking keyboard focus.
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            return cp;
        }
    }

    void ShowRandomGif()
    {
        if (gifs.Length == 0) return;

        var path = gifs[rng.Next(gifs.Length)];
        var img = Image.FromFile(path);
        pb.Image = img;
        ImageAnimator.Animate(img, OnFrameChanged);

        var area = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(rng.Next(area.Width - Width), rng.Next(area.Height - Height));

        if (!Visible) Show();
    }

    void OnFrameChanged(object sender, EventArgs e)
    {
        ImageAnimator.UpdateFrames(pb.Image);
        pb.Invalidate();
    }

    [STAThread]
    static void Main()
    {
        // "gifs" folder lives next to the .csproj (the project root), not in
        // bin\Debug\... so it survives rebuilds and `dotnet clean`.
        string folder = Path.Combine(FindProjectRoot(), "gifs");

        Application.EnableVisualStyles();
        Application.Run(new GifPopup(folder));
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
        // Fallback: just use the .exe's own folder if no .csproj is found
        // (e.g. after publishing as a standalone exe).
        return AppContext.BaseDirectory;
    }
}
