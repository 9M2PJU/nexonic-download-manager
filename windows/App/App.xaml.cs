using System.IO;
using System.IO.Pipes;
using System.Windows;
using Microsoft.Win32;
using Nexonic.Core;

namespace Nexonic.App;

public partial class App : Application
{
    const string PipeName = "nexonic-download-manager";
    Mutex? single;
    MainWindow? window;
    System.Windows.Forms.NotifyIcon? tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        single = new Mutex(true, "Nexonic.DownloadManager.Single", out var first);
        if (!first)
        {
            // Another copy runs: wake it (it then reads the inbox) and quit.
            try { using var c = new NamedPipeClientStream(".", PipeName, PipeDirection.Out); c.Connect(1000); } catch { }
            Shutdown();
            return;
        }
        Directory.CreateDirectory(AppPaths.Support);
        RegisterBrowserHost();
        var manager = new DownloadManager();
        window = new MainWindow(manager);
        window.Show();
        tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            Text = "Nexonic Download Manager",
            Visible = true,
        };
        tray.DoubleClick += (_, _) => ShowWindow();
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Show Downloads", null, (_, _) => ShowWindow());
        menu.Items.Add("Pause All", null, (_, _) => manager.PauseAll());
        menu.Items.Add("Resume All", null, (_, _) => manager.ResumeAll());
        menu.Items.Add("Quit", null, (_, _) => { manager.Save(); tray!.Visible = false; Shutdown(); });
        tray.ContextMenuStrip = menu;
        manager.Finished += r => tray.ShowBalloonTip(3000, "Download finished", r.FileName, System.Windows.Forms.ToolTipIcon.Info);
        _ = ListenAsync(manager);
    }

    void ShowWindow()
    {
        window!.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    async Task ListenAsync(DownloadManager manager)
    {
        while (true)
        {
            using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await server.WaitForConnectionAsync();
            Dispatcher.Invoke(() => { manager.CollectInbox(); ShowWindow(); });
        }
    }

    /// <summary>Lets the browser extension find the helper (per user, no admin rights needed).</summary>
    static void RegisterBrowserHost()
    {
        var host = Path.Combine(AppContext.BaseDirectory, "nexonic-host.exe");
        if (!File.Exists(host)) return;
        var manifest = Path.Combine(AppPaths.Support, NativeMessaging.HostName + ".json");
        File.WriteAllText(manifest, NativeMessaging.Manifest(host));
        foreach (var browser in new[] { @"Google\Chrome", @"Microsoft\Edge", @"BraveSoftware\Brave-Browser", @"Chromium", @"Vivaldi" })
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\{browser}\NativeMessagingHosts\{NativeMessaging.HostName}");
            key.SetValue("", manifest);
        }
    }
}
