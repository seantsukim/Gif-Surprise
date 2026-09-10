using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

// Minimal random-GIF desktop popup.
// Put your .gif files in a "gifs" folder next to the .exe (or on your Desktop\gifs).
// It shows a small always-on-top window that never steals focus, so it won't
// interrupt whatever you're typing or clicking in another app.

class GifPopup : Form
{
    readonly PictureBox pb = new PictureBox();
    readonly Random rng = new Random();
    readonly string[] gifs;
    readonly Timer switchTimer = new Timer();

    public GifPopup(string folder)
    {
        gifs = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.gif") : Array.Empty<string>();

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
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "gifs");

        Application.EnableVisualStyles();
        Application.Run(new GifPopup(folder));
    }
}
