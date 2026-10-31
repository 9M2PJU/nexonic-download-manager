using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Nexonic.Core;

namespace Nexonic.App;

public partial class MainWindow : Window
{
    readonly DownloadManager manager;
    static readonly string[] Filters = { "All Downloads", "In Progress", "Unfinished", "Finished" };

    public MainWindow(DownloadManager manager)
    {
        InitializeComponent();
        this.manager = manager;
        Grid.ItemsSource = manager.Rows;
        foreach (var f in Filters.Concat(Enum.GetNames<FileCategory>())) Filter.Items.Add(f);
        Filter.SelectedIndex = 0;
        InputBindings.Add(new KeyBinding(new Cmd(() => OnAdd(this, new RoutedEventArgs())), Key.N, ModifierKeys.Control));
    }

    // Closing the window keeps downloads running in the tray.
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        manager.Save();
        Hide();
    }

    Row? Selected => Grid.SelectedItem as Row;

    void OnAdd(object s, RoutedEventArgs e)
    {
        var box = new TextBox { AcceptsReturn = true, Height = 160, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        if (Clipboard.ContainsText() && Clipboard.GetText().StartsWith("http")) box.Text = Clipboard.GetText();
        var ok = new Button { Content = "Download", IsDefault = true, Width = 90, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        var panel = new StackPanel { Margin = new Thickness(12) };
        panel.Children.Add(new TextBlock { Text = "Paste one or more links, one per line." });
        panel.Children.Add(box);
        panel.Children.Add(ok);
        var dlg = new Window { Title = "Add Downloads", Content = panel, Width = 520, SizeToContent = SizeToContent.Height, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        ok.Click += (_, _) => { manager.AddLinks(box.Text); dlg.Close(); };
        dlg.ShowDialog();
    }

    void OnResume(object s, RoutedEventArgs e) { if (Selected is { } r) manager.Resume(r); }
    void OnPause(object s, RoutedEventArgs e) { if (Selected is { } r) manager.Pause(r); }
    void OnRemove(object s, RoutedEventArgs e) { if (Selected is { } r) manager.Remove(r); }
    void OnPauseAll(object s, RoutedEventArgs e) => manager.PauseAll();
    void OnResumeAll(object s, RoutedEventArgs e) => manager.ResumeAll();
    void OnClear(object s, RoutedEventArgs e) => manager.ClearFinished();

    void OnOpen(object s, RoutedEventArgs e)
    {
        if (Selected?.Record.SavedPath is { } p) Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
    }

    void OnShowInFolder(object s, RoutedEventArgs e)
    {
        if (Selected?.Record.SavedPath is { } p) Process.Start("explorer.exe", $"/select,\"{p}\"");
    }

    void OnCopyLink(object s, RoutedEventArgs e) { if (Selected is { } r) Clipboard.SetText(r.Record.Url); }

    void OnFilter(object s, SelectionChangedEventArgs e)
    {
        var f = Filter.SelectedItem as string;
        CollectionViewSource.GetDefaultView(Grid.ItemsSource).Filter = o =>
        {
            var r = ((Row)o).Record;
            return f switch
            {
                "In Progress" => r.IsActive || r.Status == DownloadStatus.Queued,
                "Unfinished" => r.Status != DownloadStatus.Finished,
                "Finished" => r.Status == DownloadStatus.Finished,
                "All Downloads" or null => true,
                _ => r.Category.ToString() == f,
            };
        };
    }

    void OnDrop(object s, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.Text) is string text) manager.AddLinks(text);
    }

    void OnSettings(object s, RoutedEventArgs e)
    {
        var st = manager.Settings;
        TextBox Field(string v) => new() { Text = v, Width = 260, Margin = new Thickness(0, 2, 0, 6) };
        var conn = Field(st.Connections.ToString());
        var active = Field(st.MaxActive.ToString());
        var limit = Field(st.SpeedLimitKBps.ToString());
        var folder = Field(st.Folder);
        var sort = new CheckBox { Content = "Sort into category folders", IsChecked = st.SortIntoCategoryFolders, Margin = new Thickness(0, 4, 0, 8) };
        var save = new Button { Content = "Save", IsDefault = true, Width = 90, HorizontalAlignment = HorizontalAlignment.Right };
        var p = new StackPanel { Margin = new Thickness(12) };
        foreach (var (label, box) in new[] { ("Connections per download (1-32)", conn), ("Downloads at the same time", active), ("Speed limit in KB/s (0 = none)", limit), ("Save to folder", folder) })
        {
            p.Children.Add(new TextBlock { Text = label });
            p.Children.Add(box);
        }
        p.Children.Add(sort);
        p.Children.Add(new TextBlock { Text = "Browser extension: open your browser's extensions page, switch on Developer mode, choose Load unpacked and pick the Extension folder next to this app.", TextWrapping = TextWrapping.Wrap, Width = 260, Margin = new Thickness(0, 0, 0, 8) });
        p.Children.Add(save);
        var dlg = new Window { Title = "Settings", Content = p, SizeToContent = SizeToContent.WidthAndHeight, Owner = this, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        save.Click += (_, _) =>
        {
            manager.ApplySettings(new AppSettings
            {
                Connections = Math.Clamp(int.TryParse(conn.Text, out var c) ? c : 8, 1, 32),
                MaxActive = Math.Clamp(int.TryParse(active.Text, out var a) ? a : 3, 1, 10),
                SpeedLimitKBps = Math.Max(0, int.TryParse(limit.Text, out var l) ? l : 0),
                Folder = folder.Text.Trim().Length > 0 ? folder.Text.Trim() : st.Folder,
                SortIntoCategoryFolders = sort.IsChecked == true,
            });
            dlg.Close();
        };
        dlg.ShowDialog();
    }

    sealed class Cmd(Action run) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => run();
    }
}
