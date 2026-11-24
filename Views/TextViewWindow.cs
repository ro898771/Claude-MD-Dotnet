using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClaudeMd.Views;

/// <summary>Read-only text viewer (description or SKILL.md), built in code.</summary>
public class TextViewWindow : Window
{
    public TextViewWindow(string title, string heading, string content, bool monospace)
    {
        Title = title; Width = 640; Height = 520; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var close = new Button { Content = "Close", Style = (Style)Application.Current.FindResource("Btn"), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        close.Click += (_, _) => Close();
        var box = new TextBox
        {
            Text = content, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily(monospace ? "Consolas" : "Segoe UI"), FontSize = monospace ? 13 : 15, Padding = new Thickness(10),
            BorderBrush = (Brush)Application.Current.FindResource("Line"),
        };
        var head = new TextBlock { Text = heading, FontSize = 20, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
        var dock = new DockPanel { Margin = new Thickness(20) };
        DockPanel.SetDock(head, Dock.Top); DockPanel.SetDock(close, Dock.Bottom);
        dock.Children.Add(head); dock.Children.Add(close); dock.Children.Add(box);
        Content = dock;
    }
}
