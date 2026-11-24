using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClaudeMd.Services;
using Microsoft.Win32;

namespace ClaudeMd.Views;

/// <summary>
/// "Upload Skill" dialog. Update existing: pick the category and the skill folder. Add new: pick the category, type the name and
/// choose the SKILL.md. Either way the folder in the Box zip is Storage/Dino-{Category}-{Name}/SKILL.md, after the format check.
/// </summary>
public class UploadDialog : Window
{
    // display text -> folder token used in "Dino-<token>-<name>"
    private static readonly (string Label, string Token)[] Categories = { ("Dev", "Dev"), ("Data Format", "Data_Format"), ("Apps", "Apps") };
    private static readonly Regex NameRule = new(@"^[A-Za-z0-9][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private readonly RadioButton _modeUpdate = new() { Content = "Update existing", IsChecked = true, Margin = new Thickness(0, 0, 24, 0), FontSize = 14 };
    private readonly RadioButton _modeAdd = new() { Content = "Add new", FontSize = 14 };
    private readonly ComboBox _category = new() { Height = 34, FontSize = 14 };
    private readonly ComboBox _folder = new() { Height = 34, FontSize = 14 };
    private readonly TextBox _name = new() { Height = 34, FontSize = 14, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBox _file = new() { Height = 34, FontSize = 13, IsReadOnly = true, VerticalContentAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Consolas") };
    private readonly Button _browse = new() { Content = "Browse...", Style = (Style)Application.Current.FindResource("BtnGhost"), Height = 34 };
    private readonly StackPanel _updatePanel = new();
    private readonly StackPanel _addPanel = new();
    private readonly TextBlock _target = new() { FontFamily = new FontFamily("Consolas"), FontSize = 13, TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _report = new()
    {
        IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        FontFamily = new FontFamily("Consolas"), FontSize = 12.5, Padding = new Thickness(8), MinHeight = 120,
    };
    private readonly TextBlock _status = new() { FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly Button _upload = new() { Content = "Upload to Box", Style = (Style)Application.Current.FindResource("BtnAmber"), Margin = new Thickness(8, 0, 0, 0) };
    private readonly Button _close = new() { Content = "Cancel", Style = (Style)Application.Current.FindResource("BtnGhost") };
    private SkillCheck? _check;
    private string _finalFolder = "";
    private bool _busy;

    private bool AddMode => _modeAdd.IsChecked == true;

    public UploadDialog(IEnumerable<string> storageFolders, string? preselect)
    {
        Title = "Upload Skill to Box"; Width = 700; Height = 700; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        foreach (var c in Categories) _category.Items.Add(c.Label);
        foreach (var f in storageFolders) _folder.Items.Add(f);

        var (group, _) = MainWindow.Split(preselect ?? "");
        var ci = Array.FindIndex(Categories, c => c.Token.Equals(group, StringComparison.OrdinalIgnoreCase));
        _category.SelectedIndex = ci >= 0 ? ci : 0;
        _folder.SelectedItem = preselect is not null && _folder.Items.Contains(preselect) ? preselect : null;
        if (_folder.SelectedItem is null && _folder.Items.Count > 0) _folder.SelectedIndex = 0;

        var info = new TextBlock
        {
            Text = $"Updates the zip on Box (folder {BoxUploader.FolderId}, file v1.0.0.0.zip). Box keeps the previous version of the zip. The skill folder is always Dino-{{Category}}-{{Name}}.",
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 12),
        };
        var modes = new StackPanel { Orientation = Orientation.Horizontal };
        modes.Children.Add(_modeUpdate); modes.Children.Add(_modeAdd);

        // Update existing: the skill folder of Storage
        _updatePanel.Children.Add(Label("3. Skill folder to update"));
        _updatePanel.Children.Add(_folder);

        // Add new: the name and the SKILL.md to take
        _addPanel.Children.Add(Label("3. Application name (after Dino-<Category>-: join words with _, no '-' and no spaces, e.g. Add_Winpath)"));
        _addPanel.Children.Add(_name);
        _addPanel.Children.Add(Label("4. SKILL.md file"));
        var fileRow = new DockPanel();
        DockPanel.SetDock(_browse, Dock.Right); _browse.Margin = new Thickness(8, 0, 0, 0);
        fileRow.Children.Add(_browse); fileRow.Children.Add(_file);
        _addPanel.Children.Add(fileRow);

        var panel = new StackPanel();
        panel.Children.Add(info);
        panel.Children.Add(Label("1. What do you want to do?")); panel.Children.Add(modes);
        panel.Children.Add(Label("2. Category")); panel.Children.Add(_category);
        panel.Children.Add(_updatePanel); panel.Children.Add(_addPanel);
        panel.Children.Add(Label("Box target")); panel.Children.Add(_target);
        panel.Children.Add(Label("Format check (Dino-Dev-skill-format)"));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(_close); buttons.Children.Add(_upload);
        var bottom = new StackPanel(); bottom.Children.Add(_status); bottom.Children.Add(buttons);

        var dock = new DockPanel { Margin = new Thickness(20) };
        DockPanel.SetDock(panel, Dock.Top); DockPanel.SetDock(bottom, Dock.Bottom);
        dock.Children.Add(panel); dock.Children.Add(bottom); dock.Children.Add(_report);
        Content = dock;

        _modeUpdate.Checked += (_, _) => Refresh();
        _modeAdd.Checked += (_, _) => Refresh();
        _category.SelectionChanged += (_, _) => Refresh();
        _folder.SelectionChanged += (_, _) => Refresh();
        _name.TextChanged += (_, _) => Refresh();
        _browse.Click += (_, _) => Browse();
        _close.Click += (_, _) => Close();
        _upload.Click += async (_, _) => await UploadAsync();
        Refresh();
    }

    private static TextBlock Label(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) };

    private void Browse()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Choose the SKILL.md to add", Filter = "SKILL.md / Markdown (*.md)|*.md|All files (*.*)|*.*",
            InitialDirectory = Directory.Exists(AppPaths.StorageDir) ? AppPaths.StorageDir : "",
        };
        if (dlg.ShowDialog(this) != true) return;
        _file.Text = dlg.FileName;
        // a SKILL.md picked from a skill folder suggests that folder's name
        if (_name.Text.Trim().Length == 0 && Path.GetFileName(dlg.FileName).Equals(SkillStore.SkillFile, StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(Path.GetDirectoryName(dlg.FileName)) is { Length: > 0 } dir)
            _name.Text = MainWindow.Split(dir).Child;
        Refresh();
    }

    private void Refresh()
    {
        _check = null; _finalFolder = ""; _status.Text = "";
        _updatePanel.Visibility = AddMode ? Visibility.Collapsed : Visibility.Visible;
        _addPanel.Visibility = AddMode ? Visibility.Visible : Visibility.Collapsed;
        _upload.IsEnabled = false;
        if (_category.SelectedIndex < 0) return;
        var token = Categories[_category.SelectedIndex].Token;

        string? path; string child;
        if (AddMode)
        {
            child = _name.Text.Trim();
            path = _file.Text.Length > 0 ? _file.Text : null;
            if (child.Length == 0) { _target.Text = $"Dino-{token}-<Name>"; _report.Text = "Type the name of the new skill."; return; }
            if (!NameRule.IsMatch(child)) { _target.Text = ""; _report.Text = "ERROR    The application name may only use letters, digits and '_' - no '-' and no spaces. Join the words with '_' (e.g. Add_Winpath)."; return; }
        }
        else
        {
            if (_folder.SelectedItem is not string local) { _target.Text = ""; _report.Text = ""; return; }
            child = MainWindow.Split(local).Child;
            path = Path.Combine(AppPaths.StorageDir, local, SkillStore.SkillFile);
        }
        _finalFolder = $"Dino-{token}-{child}";
        _target.Text = $"v1.0.0.0.zip  >  Storage/{_finalFolder}/SKILL.md   ({(AddMode ? "new - must not exist yet" : "update - must exist already")})";

        if (path is null) { _report.Text = "Choose the SKILL.md file (Browse...)."; return; }
        if (!File.Exists(path)) { _report.Text = "SKILL.md not found: " + path; return; }
        _check = SkillRules.Check(File.ReadAllText(path, SkillRules.Utf8NoBom), _finalFolder);

        var sb = new System.Text.StringBuilder();
        foreach (var e in _check.Errors) sb.AppendLine("ERROR    " + e);
        foreach (var w in _check.Warnings) sb.AppendLine("WARNING  " + w);
        foreach (var f in _check.Fixes) sb.AppendLine("FIXED    " + f + " (applied to the uploaded copy)");
        if (sb.Length == 0) sb.AppendLine("OK       The format is correct.");
        else if (_check.Ok) sb.Insert(0, "OK       Can be uploaded.\n");
        else sb.Insert(0, "BLOCKED  Fix the errors in the SKILL.md, then try again.\n");
        _report.Text = sb.ToString();
        _upload.IsEnabled = _check.Ok && !_busy;
    }

    private async Task UploadAsync()
    {
        if (_check is null || !_check.Ok) return;
        var mode = AddMode ? BoxUploader.UploadMode.AddNew : BoxUploader.UploadMode.Update;
        var ask = MessageBox.Show(this,
            $"{(AddMode ? "Add" : "Update")} Storage/{_finalFolder}/SKILL.md in v1.0.0.0.zip on Box?\n\nThis adds a new version of the Box file (older versions stay in Box version history).",
            "Confirm upload", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (ask != MessageBoxResult.OK) return;

        SetBusy(true);
        _status.Foreground = (Brush)Application.Current.FindResource("Ink");
        try
        {
            var bytes = SkillRules.Utf8NoBom.GetBytes(_check.FixedText);
            var r = await BoxUploader.UploadSkillAsync(_finalFolder, bytes, mode, s => Dispatcher.Invoke(() => _status.Text = s));
            _status.Foreground = (Brush)Application.Current.FindResource("Green");
            _status.Text = $"{(r.Created ? "Added" : "Updated")} {r.Entry} in {r.ZipName} ✓  ({r.ModifiedAt}, {r.ModifiedBy})";
            _close.Content = "Close";
            _upload.IsEnabled = false; // done; change something to upload again
        }
        catch (Exception ex)
        {
            _status.Foreground = Brushes.Firebrick;
            var msg = ex.GetBaseException().Message;
            _status.Text = msg.Contains("412") || msg.Contains("precondition", StringComparison.OrdinalIgnoreCase)
                ? "Someone else updated the zip on Box meanwhile. Nothing was uploaded - click Upload again."
                : ex is InvalidOperationException && (msg.Contains("already exists") || msg.Contains("not in the Box zip")) ? msg + " Nothing was uploaded."
                : "Upload failed: " + msg;
            _upload.IsEnabled = true;
        }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy; _upload.IsEnabled = !busy && _check?.Ok == true;
        foreach (UIElement e in new UIElement[] { _modeUpdate, _modeAdd, _category, _folder, _name, _browse, _close }) e.IsEnabled = !busy;
    }
}
