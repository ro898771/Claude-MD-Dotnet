using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ClaudeMd.Services;
using ClaudeMd.Views;
using Microsoft.Win32;

namespace ClaudeMd;

public sealed class IconBrush : System.Windows.Data.IValueConverter
{
    private readonly Brush _normal;
    public IconBrush(Brush normal) => _normal = normal;
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is SolidColorBrush { Color.R: 255, Color.G: 255, Color.B: 255 } ? Brushes.White : _normal;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

public class SkillRow : INotifyPropertyChanged
{
    private int _no;
    public int No
    {
        get => _no;
        set { _no = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(No))); }
    }
    public string Folder { get; init; } = "";
    public string Group { get; init; } = "";
    public string Name { get; init; } = "";
    public Brush Accent { get; init; } = Brushes.Transparent;
    public Brush AccentText { get; init; } = Brushes.Black;
    public Brush AccentLine { get; init; } = Brushes.Transparent;
    private string _description = "";
    public string Description
    {
        get => _description;
        set { _description = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class MainWindow : Window
{
    // chip colours (background, text, border), cycled per row inside a group
    private static readonly (string Bg, string Fg, string Line)[] Accents =
    {
        ("#FEF3C7", "#92400E", "#FDE68A"), ("#FFE4E6", "#BE123C", "#FECDD3"), ("#E0F2FE", "#075985", "#BAE6FD"),
        ("#E0E7FF", "#3730A3", "#C7D2FE"), ("#F3E8FF", "#7E22CE", "#E9D5FF"), ("#DCFCE7", "#166534", "#BBF7D0"),
    };

    private static Brush B(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
    private static readonly Brush Ink = B("#1A1D29");
    private static readonly Brush Folder = B("#4F46E5");
    private static readonly Brush FileGrey = B("#6B7280");
    private readonly ObservableCollection<SkillRow> _rows = new();
    private readonly ICollectionView _view;
    private bool _editing;
    private WireFrameView _wire = null!;
    private bool _wireMode;
    private Point _treeDragStart;
    private string? _treeDragSkill;

    public MainWindow()
    {
        InitializeComponent();
        var wa = SystemParameters.WorkArea;
        Width = Math.Min(Width, wa.Width * 0.95); Height = Math.Min(Height, wa.Height * 0.95); WindowStartupLocation = WindowStartupLocation.Manual; Left = wa.Left + (wa.Width - Width) / 2; Top = wa.Top + (wa.Height - Height) / 2;
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        Title = $"Claude Skill Storage  v{v?.ToString(3)}";
        PathText.Text = AppPaths.StorageDir;
        SyncBox.Text = SkillStore.LoadDefaultSyncPath();
        _view = CollectionViewSource.GetDefaultView(_rows);
        Grid.ItemsSource = _view;
        Populate();
        BuildTree();
        StartSyncWatcher();

        // Skill Wire Frame: a second view in the right-hand column, shown with the "Skill Wire Frame" tab
        _wire = new WireFrameView { Visibility = Visibility.Collapsed };
        System.Windows.Controls.Grid.SetColumn(_wire, 2);
        BodyGrid.Children.Add(_wire);
        _wire.Status += text => Status.Text = text;
        _wire.Generated += () => { Populate(); BuildTree(); RefreshSyncState(); };
    }

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (_wire is null) return; // the buttons fire Checked while the window is still being built
        SetWireMode(ModeWire.IsChecked == true);
    }

    private void SetWireMode(bool on)
    {
        _wireMode = on;
        TablePane.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        _wire.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        UploadBtn.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        EditPanel.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        GenerateBtn.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        BuildTree(); // the warning marks (no block.json) show in the wire frame only
        if (on)
        {
            _wire.RefreshContracts();
            Status.Text = "Double-click a skill (or drag it) onto the canvas. Drag from an output dot to an input dot to link blocks.";
        }
        else Status.Text = "";
    }

    private void Generate_Click(object sender, RoutedEventArgs e) => _wire.GenerateSkill();

    private void Tree_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_wireMode) return;
        if (Tree.SelectedItem is TreeViewItem { Tag: SkillRow row }) { _wire.AddSkill(row.Folder); e.Handled = true; }
    }

    private static T? Ancestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d is not null and not T) d = System.Windows.Media.VisualTreeHelper.GetParent(d);
        return d as T;
    }

    private void Tree_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _treeDragStart = e.GetPosition(Tree);
        _treeDragSkill = Ancestor<TreeViewItem>(e.OriginalSource as DependencyObject)?.Tag is SkillRow row ? row.Folder : null;
    }

    private void Tree_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_wireMode || _treeDragSkill is null || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
        var now = e.GetPosition(Tree);
        if (Math.Abs(now.X - _treeDragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(now.Y - _treeDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var skill = _treeDragSkill; _treeDragSkill = null;
        DragDrop.DoDragDrop(Tree, new DataObject(WireFrameView.DragFormat, skill), DragDropEffects.Copy);
    }

    private const string Prefix = "Dino-";
    private const string RootName = "Dino";

    /// <summary>Folder "Dino-Dev-add-winpath" -> group "Dev", child "add-winpath".</summary>
    internal static (string Group, string Child) Split(string folder)
    {
        var s = folder.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ? folder[Prefix.Length..] : folder;
        var i = s.IndexOf('-');
        return i > 0 && i < s.Length - 1 ? (s[..i], s[(i + 1)..]) : ("Other", s);
    }

    private static UIElement Header(string iconKey, string text, int? count, bool bold, string? warn = null)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = new System.Windows.Shapes.Path
        {
            Style = (Style)Application.Current.FindResource("Ic"), Data = (Geometry)Application.Current.FindResource(iconKey),
            Width = 18, Height = 18, Margin = new Thickness(0, 0, 10, 0),
        };
        // white while the tree row is selected (its text turns white), otherwise the folder / file colour
        icon.SetBinding(System.Windows.Shapes.Shape.StrokeProperty, new Binding("Foreground")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(TreeViewItem), 1),
            Converter = new IconBrush(iconKey == "IcFile" ? FileGrey : Folder),
        });
        sp.Children.Add(icon);
        sp.Children.Add(new TextBlock { Text = text, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center });
        if (count is int n)
            sp.Children.Add(new Border
            {
                Background = B("#E5E7EB"), CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(10, 0, 0, 0),
                Child = new TextBlock { Text = n.ToString(), FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = FileGrey },
            });
        if (warn is not null)
            sp.Children.Add(new TextBlock { Text = "\u26A0", Foreground = B("#F59E0B"), FontSize = 14, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = warn });
        return sp;
    }

    private void BuildTree()
    {
        var needle = SearchBox.Text.Trim();
        bool Match(SkillRow r) => needle.Length == 0
            || r.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || r.Group.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || r.Description.Contains(needle, StringComparison.OrdinalIgnoreCase);
        Tree.Items.Clear();
        var root = new TreeViewItem { Header = Header("IcFolder", RootName, null, true), Tag = RootName, IsExpanded = true, FontWeight = FontWeights.Bold };
        foreach (var g in _rows.GroupBy(r => r.Group).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var kids = g.Where(Match).ToList();
            if (kids.Count == 0) continue;
            var node = new TreeViewItem { Header = Header("IcFolder", g.Key, kids.Count, true), Tag = g.Key, IsExpanded = true };
            foreach (var k in kids)
            {
                var warn = _wireMode && !BlockContract.Exists(k.Folder)
                    ? "No block.json: the wire frame treats this skill as one generic block (input / output of type Any). Add a block.json next to its SKILL.md to give it real ports."
                    : null;
                node.Items.Add(new TreeViewItem { Header = Header("IcFile", k.Name, null, false, warn), Tag = k });
            }
            root.Items.Add(node);
        }
        Tree.Items.Add(root);
        root.IsSelected = true;
    }

    private void ShowGroup(string? group, SkillRow? pick, bool all = false)
    {
        var n = 0;
        foreach (var r in _rows.Where(r => all || r.Group == group)) r.No = ++n;
        _view.Filter = o => all || ((SkillRow)o).Group == group;
        _view.Refresh();
        GroupTitle.Text = all ? "All skills" : group ?? "Dino";
        GroupCount.Text = group is null && !all ? $"{_rows.Count} skills in {_rows.Select(r => r.Group).Distinct().Count()} groups" : $"{n} skills";
        if (pick != null) Grid.SelectedItem = pick;
    }

    private void ShowAll_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        if (Tree.SelectedItem is TreeViewItem t) t.IsSelected = false;
        ShowGroup(null, null, all: true);
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(PathText.Text); Status.Text = "Path copied ✓"; } catch (System.Runtime.InteropServices.ExternalException) { }
    }

    private void Browse_Click(object sender, RoutedEventArgs e) => SyncBox_MouseDoubleClick(sender, null!);

    private void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not TreeViewItem { Tag: var tag }) return;
        if (tag is SkillRow r) ShowGroup(r.Group, r);
        else if (tag is string s && s != RootName) ShowGroup(s, null);
        else ShowGroup(null, null);
    }

    private void Populate()
    {
        _rows.Clear();
        var i = 0;
        var perGroup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in SkillStore.Load(AppPaths.StorageDir))
        {
            var (group, child) = Split(e.Name);
            perGroup[group] = perGroup.GetValueOrDefault(group);
            var (bg, fg, line) = Accents[perGroup[group]++ % Accents.Length];
            _rows.Add(new SkillRow
            {
                No = ++i, Folder = e.Name, Group = group, Name = child, Description = e.Description,
                Accent = B(bg), AccentText = B(fg), AccentLine = B(line),
            });
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_view != null) BuildTree();
    }

    private void SetEditing(bool on)
    {
        _editing = on;
        Grid.IsReadOnly = !on;
        EditText.Text = on ? "Cancel" : "Edit";
        SaveBtn.IsEnabled = on;
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (!_editing) { SetEditing(true); Status.Text = "Editing enabled — double-click a Description cell, then Save."; return; }
        Grid.CancelEdit();
        SetEditing(false);
        Status.Text = "";
        Populate();
        BuildTree();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Grid.CommitEdit(DataGridEditingUnit.Row, true);
        foreach (var r in _rows)
            SkillStore.WriteDescription(Path.Combine(AppPaths.StorageDir, r.Folder, SkillStore.SkillFile), r.Description.Trim());
        SetEditing(false);
        Status.Text = "Saved ✓";
        RefreshSyncState();
    }

    private void Grid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_editing || Grid.CurrentColumn?.Header as string != "DESCRIPTION" || Grid.SelectedItem is not SkillRow r) return;
        new TextViewWindow($"{r.Name} — Description", r.Name, r.Description, false) { Owner = this }.ShowDialog();
    }

    private void ViewSkill_Click(object sender, RoutedEventArgs e)
    {
        var folder = (string)((Button)sender).Tag;
        var name = Split(folder).Child;
        var path = Path.Combine(AppPaths.StorageDir, folder, SkillStore.SkillFile);
        var text = File.Exists(path) ? File.ReadAllText(path) : "SKILL.md not found.";
        new TextViewWindow($"{name} — SKILL.md", name, text, true) { Owner = this }.ShowDialog();
    }

    private void SyncBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select Destination Directory", InitialDirectory = SyncBox.Text.Trim() };
        if (dlg.ShowDialog(this) == true) SyncBox.Text = dlg.FolderName;
    }

    /// <summary>Updates the Sync button to show how many Storage files differ from the target.</summary>
    private void RefreshSyncState()
    {
        int pending;
        try { pending = SkillStore.CountPending(AppPaths.StorageDir, SyncBox.Text.Trim()); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { pending = 0; }
        SyncText.Text = pending > 0 ? $"Sync ({pending} changed)" : "Sync";
        SyncBtn.ToolTip = pending > 0 ? $"{pending} file(s) differ from the target — click to update" : "Target is up to date";
    }

    private void StartSyncWatcher()
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => RefreshSyncState();
        timer.Start();
        SyncBox.TextChanged += (_, _) => RefreshSyncState();
        RefreshSyncState();
    }

    private void Upload_Click(object sender, RoutedEventArgs e)
    {
        var folders = SkillStore.Load(AppPaths.StorageDir).Select(x => x.Name).ToList();
        if (folders.Count == 0) { SyncStatus.Text = "Storage has no skills to upload."; return; }
        var pick = (Grid.SelectedItem as SkillRow)?.Folder;
        new UploadDialog(folders, pick) { Owner = this }.ShowDialog();
    }

    private void Sync_Click(object sender, RoutedEventArgs e)
    {
        var target = SyncBox.Text.Trim();
        if (target.Length == 0) target = SkillStore.LoadDefaultSyncPath();
        if (target.Length == 0) { SyncStatus.Text = "Enter a destination path first."; return; }
        try
        {
            var (final, copied) = SkillStore.Sync(AppPaths.StorageDir, target);
            SyncBox.Text = final;
            SkillStore.SaveDefaultSyncPath(final);
            SyncStatus.Text = $"Synced to {final} — {copied} file(s) updated ✓";
            RefreshSyncState();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SyncStatus.Text = $"Sync failed: {ex.Message}";
        }
    }
}
