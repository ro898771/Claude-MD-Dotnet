using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClaudeMd.Views;

/// <summary>Asks for the application name (the part after Dino-Apps-) and the one-line description of the generated workflow skill.</summary>
public sealed class WorkflowNameDialog : Window
{
    private static readonly Regex NameRule = new(@"^[A-Za-z0-9][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private readonly TextBox _name = new() { Height = 34, FontSize = 14, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBox _desc = new() { FontSize = 13, TextWrapping = TextWrapping.Wrap, AcceptsReturn = false, MinHeight = 64, Padding = new Thickness(4) };
    private readonly TextBlock _folder = new() { FontFamily = new FontFamily("Consolas"), FontSize = 13, Margin = new Thickness(0, 6, 0, 0) };
    private readonly TextBlock _message = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), FontWeight = FontWeights.SemiBold };
    private readonly Button _ok = new() { Content = "Generate", Style = (Style)Application.Current.FindResource("BtnAmber"), Margin = new Thickness(8, 0, 0, 0) };
    private readonly Button _cancel = new() { Content = "Cancel", Style = (Style)Application.Current.FindResource("BtnGhost") };

    public string AppName => _name.Text.Trim();
    public string Description => _desc.Text.Trim();

    private WorkflowNameDialog(string name, string description)
    {
        Title = "Generate Skill"; Width = 560; Height = 400; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        _name.Text = name; _desc.Text = description;

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = "New workflow skill", FontSize = 18, FontWeight = FontWeights.Bold });
        panel.Children.Add(Label("Application name (after Dino-Apps-: words joined with _, no '-' and no spaces)"));
        panel.Children.Add(_name);
        panel.Children.Add(_folder);
        panel.Children.Add(Label("Description (one line: what it does. Use when ...)"));
        panel.Children.Add(_desc);
        panel.Children.Add(_message);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        buttons.Children.Add(_cancel); buttons.Children.Add(_ok);
        panel.Children.Add(buttons);
        Content = panel;

        _name.TextChanged += (_, _) => Check();
        _desc.TextChanged += (_, _) => Check();
        _cancel.Click += (_, _) => { DialogResult = false; };
        _ok.Click += (_, _) => { DialogResult = true; };
        Loaded += (_, _) => { _name.Focus(); _name.SelectAll(); };
        Check();
    }

    private static TextBlock Label(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 4), TextWrapping = TextWrapping.Wrap };

    private void Check()
    {
        _folder.Text = AppName.Length == 0 ? "Dino-Apps-<Name>" : $"Dino-Apps-{AppName}";
        string? problem = null;
        if (AppName.Length == 0) problem = "Type the application name.";
        else if (!NameRule.IsMatch(AppName)) problem = "The name may only use letters, digits and '_' - no '-' and no spaces. Join the words with '_' (e.g. GU_To_Report).";
        else if (Description.Length == 0) problem = "Type a one-line description.";
        else if (Description.Contains('\n') || Description.Contains('\r')) problem = "The description must be one single line.";
        _message.Text = problem ?? "";
        _ok.IsEnabled = problem is null;
    }

    /// <summary>Null when the user cancels.</summary>
    public static (string Name, string Description)? Ask(Window? owner, string name, string description)
    {
        var d = new WorkflowNameDialog(name, description) { Owner = owner };
        return d.ShowDialog() == true ? (d.AppName, d.Description) : null;
    }
}
