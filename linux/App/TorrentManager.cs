using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using Nexonic.Core;
using Nexonic.Linux;

namespace Nexonic.App;

public static partial class Dialogs
{
    sealed record TorrentManagerRow(Row Row)
    {
        public string Label => $"{Row.Record.FileName} · {Row.Status} · {Row.Percent:0}%\n{TorrentText.Summary(Row.Record.Torrent!)}";
    }
    public static async Task TorrentManager(Window owner, DownloadManager manager, string queueId)
    {
        var f = new Form(owner, "Torrent Manager", 900);
        f.Note("Pause and resume use verified native checkpoints. Removing a task retains downloaded files. Select a torrent to manage its files, trackers, seeding and limits.");
        var list = f.Field("Torrents", new ListBox { Height = 280 });
        list.ItemTemplate = new FuncDataTemplate<TorrentManagerRow>((r, _) => new TextBlock { Text = r?.Label });
        Guid? selected = null;
        void Update()
        {
            selected = (list.SelectedItem as TorrentManagerRow)?.Row.Record.Id ?? selected;
            var rows = manager.Rows.Where(r => r.Record.Torrent != null).Select(r => new TorrentManagerRow(r)).ToList();
            list.ItemsSource = rows; list.SelectedItem = rows.FirstOrDefault(r => r.Row.Record.Id == selected);
        }
        Row? Pick() => (list.SelectedItem as TorrentManagerRow)?.Row;
        f.AddButton("Add Torrent", async () => await Torrent(f.Window, manager, "", queueId));
        f.AddButton("Pause", () => { if (Pick() is { } r) manager.Pause(r); });
        f.AddButton("Resume", () => { if (Pick() is { } r) manager.Resume(r); });
        f.AddButton("Files and Settings", async () => { if (Pick() is { } r) await TorrentFiles(f.Window, manager, r); });
        f.AddButton("Remove", () => { if (Pick() is { } r) manager.Remove(r); });
        f.AddButton("Close", f.Close);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; timer.Tick += (_, _) => Update();
        f.Window.Closed += (_, _) => timer.Stop(); Update(); timer.Start(); await f.Show();
    }
}
