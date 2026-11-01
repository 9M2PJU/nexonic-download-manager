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
        menu.Items.Add("Add URL…", null, (_, _) => window!.ShowAdd(Dialogs.ClipboardLink()));
        menu.Items.Add("Stop All", null, (_, _) => manager.PauseAll());
        menu.Items.Add("Resume All", null, (_, _) => manager.ResumeAll());
        menu.Items.Add("Quit", null, (_, _) => Quit(manager));
        tray.ContextMenuStrip = menu;
        manager.Notify += (title, body) =>
        {
            if (manager.Settings.Notifications) tray.ShowBalloonTip(3000, title, body, System.Windows.Forms.ToolTipIcon.Info);
        };
        manager.Finished += r =>
        {
            if (manager.Settings.Notifications)
                tray.ShowBalloonTip(3000, "Download finished", r.FileName + (r.ChecksumResult is { } c ? " – " + c : ""), System.Windows.Forms.ToolTipIcon.Info);
            if (manager.Settings.ShowCompleteDialog) Dialogs.Complete(manager, r);
        };
        manager.ClipboardLink += link => window.ShowAdd(link);
        manager.QuitRequested += () => Quit(manager);
        Demo.Start(manager, window);
        _ = ListenAsync(manager);
    }

    void Quit(DownloadManager manager)
    {
        manager.Save();
        if (tray != null) tray.Visible = false;
        Shutdown();
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
        var firefox = Path.Combine(AppPaths.Support, NativeMessaging.HostName + ".firefox.json");
        File.WriteAllText(firefox, NativeMessaging.FirefoxManifest(host));
        using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Mozilla\NativeMessagingHosts\{NativeMessaging.HostName}"))
            key.SetValue("", firefox);
    }
}
