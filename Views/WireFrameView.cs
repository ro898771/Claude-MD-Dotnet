using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using ClaudeMd.Services;
using Microsoft.Win32;
using IOPath = System.IO.Path;
using Path = System.Windows.Shapes.Path;

namespace ClaudeMd.Views;

/// <summary>
/// Skill Wire Frame: a canvas where each skill of Storage is a block (its inputs on the left, outputs on the right, read from
/// block.json). Drag from an output dot to an input dot to link two blocks. Generate Skill turns the wiring into one new
/// Dino-Apps-{Name} SKILL.md that runs the skills in order.
/// </summary>
public sealed class WireFrameView : UserControl
{
    public const string DragFormat = "SkillFolder";

    private static Brush B(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
    private static readonly Brush Ink = B("#1A1D29"), LineBrush = B("#E5E7EB"), Accent = B("#4F46E5"), Amber = B("#F59E0B"),
        Green = B("#16A34A"), Muted = B("#6B7280"), ChipBlue = B("#EEF0FB"), ChipGrey = B("#F3F4F6"), Danger = B("#DC2626");

    private sealed record PortRef(NodeVis Node, string Name, bool IsInput);

    private sealed class NodeVis
    {
        public WfNode Model = null!;
        public BlockContract Contract = null!;
        public Grid Root = null!;
        public Border Header = null!;
        public bool Missing;
        public Dictionary<string, Ellipse> In = new();
        public Dictionary<string, Ellipse> Out = new();
        public Dictionary<(bool IsInput, string Name), Point> Offset = new();
    }

    private sealed class LinkVis
    {
        public WfLink Model = null!;
        public Path Line = null!;
        public Path Hit = null!;
    }

    private WfFile _wf = new();
    private readonly List<NodeVis> _nodes = new();
    private readonly List<LinkVis> _links = new();

    private readonly Grid _viewport = new() { ClipToBounds = true, AllowDrop = true, Background = DotBackground() };
    private readonly Canvas _world = new();
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _pan = new();
    private readonly TextBlock _zoomText = new() { Text = "100%", FontWeight = FontWeights.SemiBold, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Width = 48, TextAlignment = TextAlignment.Center };
    private readonly TextBlock _hint = new()
    {
        Text = "Double-click a skill in the explorer, or drag it here, to add a block.\nDrag from an output dot to an input dot to link two blocks.",
        TextAlignment = TextAlignment.Center, FontSize = 15, Foreground = B("#9CA3AF"), IsHitTestVisible = false,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
    };

    private Path? _temp;
    private PortRef? _dragFrom;
    private bool _panning;
    private Point _panStart, _panOrigin;
    private int _z = 10;

    public event Action<string>? Status;
    public event Action? Generated;

    public WireFrameView()
    {
        _world.RenderTransform = new TransformGroup { Children = { _scale, _pan } };
        _viewport.Children.Add(_world);
        _viewport.Children.Add(_hint);
        _viewport.Children.Add(BuildToolbar());
        _viewport.Children.Add(BuildZoomBar());
        Content = _viewport;

        _viewport.MouseLeftButtonDown += ViewportDown;
        _viewport.MouseMove += ViewportMove;
        _viewport.MouseLeftButtonUp += ViewportUp;
        _viewport.MouseWheel += ViewportWheel;
        _viewport.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DragFormat) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        _viewport.Drop += (_, e) =>
        {
            if (e.Data.GetData(DragFormat) is string skill)
            {
                var p = e.GetPosition(_world);
                AddSkill(skill, new Point(p.X - 150, p.Y - 20));
            }
        };
        UpdateHint();
    }

    private void Say(string message) => Status?.Invoke(message);

    private static Brush DotBackground()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 24, 24))));
        group.Children.Add(new GeometryDrawing(B("#D1D5DB"), null, new EllipseGeometry(new Point(2, 2), 1.1, 1.1)));
        var brush = new DrawingBrush(group)
        {
            TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 24, 24), ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 24, 24), ViewboxUnits = BrushMappingMode.Absolute,
        };
        brush.Freeze();
        return brush;
    }

    // ---------------------------------------------------------------------------------------- toolbar and zoom bar

    private static readonly Style Flat = (Style)XamlReader.Parse(
        "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>" +
        "<Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>" +
        "<Border x:Name='bd' Background='Transparent' CornerRadius='8' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>" +
        "<ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='Background' Value='#EEF0FB'/></Trigger></ControlTemplate.Triggers>" +
        "</ControlTemplate></Setter.Value></Setter></Style>");

    private static Button FlatButton(string text, RoutedEventHandler click, bool accent = false, string? tip = null)
    {
        var b = new Button
        {
            Content = text, Style = Flat, Padding = new Thickness(14, 8, 14, 8), FontSize = 14, FontWeight = accent ? FontWeights.SemiBold : FontWeights.Medium,
            Foreground = accent ? Accent : Ink, ToolTip = tip,
        };
        b.Click += click;
        return b;
    }

    private static Border Card(UIElement child, Thickness margin, HorizontalAlignment h, VerticalAlignment v)
    {
        var card = new Border
        {
            Background = Brushes.White, BorderBrush = LineBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(6),
            Margin = margin, HorizontalAlignment = h, VerticalAlignment = v, Child = child,
            Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Opacity = 0.10, Color = Colors.Black },
        };
        card.MouseLeftButtonDown += (_, e) => e.Handled = true; // clicking the bar must not start a pan
        return card;
    }

    private UIElement BuildToolbar()
    {
        var chip = new Border
        {
            Background = ChipBlue, CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 4, 0),
            Child = new TextBlock { Text = "Skill Wire Frame", FontWeight = FontWeights.SemiBold, FontSize = 14, Foreground = Accent },
        };
        var bar = new StackPanel { Orientation = Orientation.Horizontal };
        bar.Children.Add(chip);
        bar.Children.Add(new Border { Width = 1, Background = LineBrush, Margin = new Thickness(4, 6, 4, 6) });
        bar.Children.Add(FlatButton("Sample", (_, _) => LoadSample(), tip: "Load an example with your own skills"));
        bar.Children.Add(FlatButton("Save JSON", (_, _) => SaveJson(), tip: "Save the wiring to a .json file"));
        bar.Children.Add(FlatButton("Open", (_, _) => OpenJson(), tip: "Open a saved .json wiring"));
        bar.Children.Add(FlatButton("Clear", (_, _) => ClearAll(), tip: "Remove every block"));
        bar.Children.Add(new Border { Width = 1, Background = LineBrush, Margin = new Thickness(4, 6, 4, 6) });
        bar.Children.Add(FlatButton("Run Tests", (_, _) => RunTests(), accent: true, tip: "Check the links, the types and the generated skill"));
        return Card(bar, new Thickness(16, 16, 0, 0), HorizontalAlignment.Left, VerticalAlignment.Top);
    }

    private UIElement BuildZoomBar()
    {
        var bar = new StackPanel { Orientation = Orientation.Horizontal };
        bar.Children.Add(FlatButton("-", (_, _) => ZoomBy(1 / 1.2), tip: "Zoom out"));
        bar.Children.Add(_zoomText);
        bar.Children.Add(FlatButton("+", (_, _) => ZoomBy(1.2), tip: "Zoom in"));
        bar.Children.Add(FlatButton("Reset", (_, _) => { _scale.ScaleX = _scale.ScaleY = 1; _pan.X = _pan.Y = 0; UpdateZoomText(); }, tip: "100% and back to the top left"));
        return Card(bar, new Thickness(0, 0, 16, 16), HorizontalAlignment.Right, VerticalAlignment.Bottom);
    }

    private void UpdateZoomText() => _zoomText.Text = $"{_scale.ScaleX * 100:0}%";

    private void ZoomBy(double factor) => ZoomAt(factor, new Point(_viewport.ActualWidth / 2, _viewport.ActualHeight / 2));

    private void ZoomAt(double factor, Point at)
    {
        var s = Math.Clamp(_scale.ScaleX * factor, 0.35, 2.2);
        var world = new Point((at.X - _pan.X) / _scale.ScaleX, (at.Y - _pan.Y) / _scale.ScaleX); // the point under the cursor stays there
        _scale.ScaleX = _scale.ScaleY = s;
        _pan.X = at.X - world.X * s;
        _pan.Y = at.Y - world.Y * s;
        UpdateZoomText();
    }

    // ---------------------------------------------------------------------------------------- viewport mouse

    private void ViewportDown(object sender, MouseButtonEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, _viewport)) return; // only the empty canvas pans
        _panning = true;
        _panStart = e.GetPosition(_viewport);
        _panOrigin = new Point(_pan.X, _pan.Y);
        _viewport.CaptureMouse();
        _viewport.Cursor = Cursors.Hand;
        e.Handled = true;
    }

    private void ViewportMove(object sender, MouseEventArgs e)
    {
        if (_dragFrom is not null) DrawTemp(e.GetPosition(_world));
        else if (_panning)
        {
            var p = e.GetPosition(_viewport);
            _pan.X = _panOrigin.X + p.X - _panStart.X;
            _pan.Y = _panOrigin.Y + p.Y - _panStart.Y;
        }
    }

    private void ViewportUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragFrom is not null)
        {
            var from = _dragFrom;
            _dragFrom = null;
            if (_temp is not null) { _world.Children.Remove(_temp); _temp = null; }
            var target = NearestInput(e.GetPosition(_world), from.Node);
            if (target is not null) TryLink(from, target);
            else Say("Link cancelled: drop it on an input dot (left side of a block).");
        }
        if (_panning) { _panning = false; _viewport.Cursor = null; }
        if (_viewport.IsMouseCaptured) _viewport.ReleaseMouseCapture();
    }

    private void ViewportWheel(object sender, MouseWheelEventArgs e)
    {
        ZoomAt(Math.Pow(1.1, e.Delta / 120.0), e.GetPosition(_viewport));
        e.Handled = true;
    }

    // ---------------------------------------------------------------------------------------- blocks

    private string NextId()
    {
        int max = _wf.Nodes.Select(n => int.TryParse(n.Id.TrimStart('n'), out var v) ? v : 0).DefaultIfEmpty(0).Max();
        return "n" + (max + 1);
    }

    private Point NextFreeSpot()
    {
        double off = (_nodes.Count % 8) * 28;
        if (_viewport.ActualWidth < 50) return new Point(80 + off, 80 + off);
        var center = new Point((_viewport.ActualWidth / 2 - _pan.X) / _scale.ScaleX, (_viewport.ActualHeight / 2 - _pan.Y) / _scale.ScaleX);
        return new Point(center.X - 152 + off, center.Y - 60 + off);
    }

    /// <summary>Adds the skill as a new block (at the centre of the view when no place is given).</summary>
    public void AddSkill(string skill, Point? at = null)
    {
        var p = at ?? NextFreeSpot();
        var node = new WfNode { Id = NextId(), Skill = skill, X = Math.Round(p.X), Y = Math.Round(p.Y) };
        _wf.Nodes.Add(node);
        AddNodeVis(node);
        UpdateHint();
        Say($"Added {skill}.");
    }

    private void UpdateHint() => _hint.Visibility = _wf.Nodes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private static TextBlock Text(string text, double size, Brush brush, bool mono = false, FontWeight? weight = null) => new()
    {
        Text = text, FontSize = size, Foreground = brush, FontWeight = weight ?? FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center,
        FontFamily = mono ? new FontFamily("Consolas") : new FontFamily("Segoe UI"),
    };

    private static Border Chip(string type, bool input) => new()
    {
        Background = input ? ChipGrey : ChipBlue, CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 1), VerticalAlignment = VerticalAlignment.Center,
        Child = Text(type, 11, input ? Muted : Accent, mono: true),
    };

    private Grid PortRow(NodeVis nv, Port port, bool input)
    {
        var row = new Grid { Height = 30, Margin = new Thickness(0, 1, 0, 1) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dot = new Ellipse
        {
            Width = 12, Height = 12, Fill = input ? Amber : Green, Stroke = Brushes.White, StrokeThickness = 2, Cursor = Cursors.Cross,
            VerticalAlignment = VerticalAlignment.Center, ToolTip = $"{port.Name} ({port.Type})" + (input ? "" : "  -  drag to an input to link"),
            Tag = new PortRef(nv, port.Name, input),
        };
        var name = Text(port.Name, 12.5, Ink, mono: true);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        var chip = Chip(port.Type, input);

        if (input)
        {
            dot.HorizontalAlignment = HorizontalAlignment.Left; dot.Margin = new Thickness(-6, 0, 0, 0);
            name.Margin = new Thickness(14, 0, 8, 0); chip.Margin = new Thickness(0, 0, 12, 0);
            Grid.SetColumn(dot, 0); Grid.SetColumn(name, 1); Grid.SetColumn(chip, 2);
            nv.In[port.Name] = dot;
        }
        else
        {
            chip.Margin = new Thickness(12, 0, 0, 0); name.Margin = new Thickness(8, 0, 14, 0); name.HorizontalAlignment = HorizontalAlignment.Right;
            dot.HorizontalAlignment = HorizontalAlignment.Right; dot.Margin = new Thickness(0, 0, -6, 0);
            Grid.SetColumn(chip, 0); Grid.SetColumn(name, 1); Grid.SetColumn(dot, 2);
            nv.Out[port.Name] = dot;
            dot.MouseLeftButtonDown += (_, e) => BeginLink((PortRef)dot.Tag, e);
        }
        if (input) dot.MouseLeftButtonDown += (_, e) => e.Handled = true;
        row.Children.Add(dot); row.Children.Add(name); row.Children.Add(chip);
        return row;
    }

    private NodeVis BuildNodeVis(WfNode node)
    {
        var nv = new NodeVis { Model = node, Contract = BlockContract.Load(node.Skill), Missing = !BlockContract.SkillExists(node.Skill) };

        var title = Text(node.Skill + (nv.Missing ? "  (missing)" : ""), 12, nv.Missing ? Danger : Ink, mono: true, weight: FontWeights.SemiBold);
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        var close = Text("×", 18, Muted);
        close.Cursor = Cursors.Hand; close.Margin = new Thickness(8, -3, 0, 0); close.ToolTip = "Remove this block";
        close.MouseLeftButtonDown += (_, e) => e.Handled = true;
        close.MouseLeftButtonUp += (_, e) => { RemoveNode(nv); e.Handled = true; };
        close.MouseEnter += (_, _) => close.Foreground = Danger;
        close.MouseLeave += (_, _) => close.Foreground = Muted;
        Grid.SetColumn(close, 1);
        var headGrid = new Grid();
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headGrid.Children.Add(title); headGrid.Children.Add(close);
        nv.Header = new Border { Background = Brushes.Transparent, Padding = new Thickness(14, 10, 10, 6), Cursor = Cursors.SizeAll, Child = headGrid };
        WireHeaderDrag(nv);

        var rows = new StackPanel { Margin = new Thickness(0, 2, 0, 10) };
        foreach (var p in nv.Contract.Inputs) rows.Children.Add(PortRow(nv, p, true));
        foreach (var p in nv.Contract.Outputs) rows.Children.Add(PortRow(nv, p, false));

        var tip = new StringBuilder();
        var description = nv.Missing ? "" : SkillStore.ReadDescription(IOPath.Combine(AppPaths.StorageDir, node.Skill, SkillStore.SkillFile));
        if (description.Length > 0) tip.AppendLine(description);
        if (!nv.Contract.HasContract) tip.Append("No block.json: one generic input and output (type Any).");
        else if (nv.Contract.Problem.Length > 0) tip.Append(nv.Contract.Problem);

        var stack = new StackPanel();
        stack.Children.Add(nv.Header); stack.Children.Add(rows);
        var card = new Border
        {
            Background = Brushes.White, BorderBrush = nv.Missing ? Danger : (nv.Contract.HasContract ? LineBrush : Amber), BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(10),
            Margin = new Thickness(7, 0, 7, 0), Width = 290, Child = stack,
            Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Opacity = 0.10, Color = Colors.Black },
            ToolTip = tip.Length > 0 ? tip.ToString().Trim() : null,
        };
        nv.Root = new Grid { Width = 304, Children = { card } };
        Canvas.SetLeft(nv.Root, node.X); Canvas.SetTop(nv.Root, node.Y); Panel.SetZIndex(nv.Root, ++_z);

        // where each dot sits inside the block (the dots do not move relative to it), so links can follow a dragged block without a layout pass
        nv.Root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        nv.Root.Arrange(new Rect(nv.Root.DesiredSize));
        foreach (var (name, dot) in nv.In) nv.Offset[(true, name)] = dot.TransformToVisual(nv.Root).Transform(new Point(6, 6));
        foreach (var (name, dot) in nv.Out) nv.Offset[(false, name)] = dot.TransformToVisual(nv.Root).Transform(new Point(6, 6));
        return nv;
    }

    private void AddNodeVis(WfNode node)
    {
        var nv = BuildNodeVis(node);
        _nodes.Add(nv);
        _world.Children.Add(nv.Root);
        RefreshPortColors();
    }

    private void WireHeaderDrag(NodeVis nv)
    {
        Point start = default, origin = default;
        nv.Header.MouseLeftButtonDown += (_, e) =>
        {
            start = e.GetPosition(_viewport); origin = new Point(nv.Model.X, nv.Model.Y);
            Panel.SetZIndex(nv.Root, ++_z);
            nv.Header.CaptureMouse(); e.Handled = true;
        };
        nv.Header.MouseMove += (_, e) =>
        {
            if (!nv.Header.IsMouseCaptured) return;
            var p = e.GetPosition(_viewport);
            nv.Model.X = Math.Round(origin.X + (p.X - start.X) / _scale.ScaleX);
            nv.Model.Y = Math.Round(origin.Y + (p.Y - start.Y) / _scale.ScaleX);
            Canvas.SetLeft(nv.Root, nv.Model.X); Canvas.SetTop(nv.Root, nv.Model.Y);
            foreach (var l in _links.Where(l => l.Model.FromNode == nv.Model.Id || l.Model.ToNode == nv.Model.Id)) UpdateLink(l);
        };
        nv.Header.MouseLeftButtonUp += (_, e) => { if (nv.Header.IsMouseCaptured) nv.Header.ReleaseMouseCapture(); e.Handled = true; };
    }

    private void RemoveNode(NodeVis nv)
    {
        foreach (var l in _links.Where(l => l.Model.FromNode == nv.Model.Id || l.Model.ToNode == nv.Model.Id).ToList()) RemoveLinkVis(l);
        _world.Children.Remove(nv.Root);
        _nodes.Remove(nv);
        _wf.Nodes.Remove(nv.Model);
        RefreshPortColors();
        UpdateHint();
        Say($"Removed {nv.Model.Skill}.");
    }

    // ---------------------------------------------------------------------------------------- links

    private Point PortCenter(NodeVis nv, string port, bool input)
    {
        var o = nv.Offset.TryGetValue((input, port), out var off) ? off : new Point(0, 0);
        return new Point(nv.Model.X + o.X, nv.Model.Y + o.Y);
    }

    private static Geometry Curve(Point a, Point b)
    {
        double d = Math.Max(50, Math.Abs(b.X - a.X) / 2);
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(a, false, false);
            ctx.BezierTo(new Point(a.X + d, a.Y), new Point(b.X - d, b.Y), b, true, true);
        }
        g.Freeze();
        return g;
    }

    private void BeginLink(PortRef from, MouseButtonEventArgs e)
    {
        _dragFrom = from;
        _temp = new Path { Stroke = Accent, StrokeThickness = 2.5, StrokeDashArray = new DoubleCollection { 4, 3 }, IsHitTestVisible = false };
        Panel.SetZIndex(_temp, 1000);
        _world.Children.Add(_temp);
        _viewport.CaptureMouse();
        DrawTemp(e.GetPosition(_world));
        e.Handled = true;
    }

    private void DrawTemp(Point mouse)
    {
        if (_dragFrom is null || _temp is null) return;
        _temp.Data = Curve(PortCenter(_dragFrom.Node, _dragFrom.Name, false), mouse);
    }

    /// <summary>The input dot closest to the point (within 24 px), so a drop does not have to hit the small dot exactly.</summary>
    private PortRef? NearestInput(Point world, NodeVis except)
    {
        PortRef? best = null; double bestDistance = 24;
        foreach (var nv in _nodes.Where(n => n != except))
            foreach (var name in nv.In.Keys)
            {
                var c = PortCenter(nv, name, true);
                var dist = Math.Sqrt((c.X - world.X) * (c.X - world.X) + (c.Y - world.Y) * (c.Y - world.Y));
                if (dist < bestDistance) { bestDistance = dist; best = new PortRef(nv, name, true); }
            }
        return best;
    }

    private bool TryLink(PortRef from, PortRef to)
    {
        if (ReferenceEquals(from.Node, to.Node)) { Say("A block cannot feed itself."); return false; }
        var op = from.Node.Contract.Outputs.First(p => p.Name == from.Name);
        var ip = to.Node.Contract.Inputs.First(p => p.Name == to.Name);
        if (!WorkflowRules.TypesMatch(op.Type, ip.Type)) { Say($"These ports do not match: {op.Type} cannot go into {ip.Type}."); return false; }

        var link = new WfLink { FromNode = from.Node.Model.Id, FromPort = from.Name, ToNode = to.Node.Model.Id, ToPort = to.Name };
        var trial = _wf.Clone();
        trial.Links.RemoveAll(l => l.ToNode == link.ToNode && l.ToPort == link.ToPort);
        trial.Links.Add(link);
        if (WorkflowRules.Order(trial) is null) { Say("This link would make a loop; a workflow must run from start to end."); return false; }

        foreach (var old in _links.Where(l => l.Model.ToNode == link.ToNode && l.Model.ToPort == link.ToPort).ToList()) RemoveLinkVis(old); // an input takes one link
        _wf.Links.Add(link);
        AddLinkVis(link);
        RefreshPortColors();
        Say($"Linked {from.Node.Model.Skill}.{from.Name} ({op.Type}) to {to.Node.Model.Skill}.{to.Name}.");
        return true;
    }

    private void AddLinkVis(WfLink model)
    {
        var from = _nodes.FirstOrDefault(n => n.Model.Id == model.FromNode);
        var to = _nodes.FirstOrDefault(n => n.Model.Id == model.ToNode);
        if (from is null || to is null || !from.Out.ContainsKey(model.FromPort) || !to.In.ContainsKey(model.ToPort)) return; // Run Tests reports it

        var lv = new LinkVis
        {
            Model = model,
            Line = new Path { Stroke = Accent, StrokeThickness = 2.5, IsHitTestVisible = false },
            Hit = new Path { Stroke = Brushes.Transparent, StrokeThickness = 16, Cursor = Cursors.Hand, ToolTip = "Double-click or right-click to remove this link" },
        };
        Panel.SetZIndex(lv.Line, 1); Panel.SetZIndex(lv.Hit, 2);
        lv.Hit.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) { RemoveLinkVis(lv); RefreshPortColors(); Say("Link removed."); } e.Handled = true; };
        lv.Hit.MouseRightButtonUp += (_, e) => { RemoveLinkVis(lv); RefreshPortColors(); Say("Link removed."); e.Handled = true; };
        _links.Add(lv);
        _world.Children.Add(lv.Line); _world.Children.Add(lv.Hit);
        UpdateLink(lv);
    }

    private void UpdateLink(LinkVis lv)
    {
        var from = _nodes.FirstOrDefault(n => n.Model.Id == lv.Model.FromNode);
        var to = _nodes.FirstOrDefault(n => n.Model.Id == lv.Model.ToNode);
        if (from is null || to is null) return;
        var geometry = Curve(PortCenter(from, lv.Model.FromPort, false), PortCenter(to, lv.Model.ToPort, true));
        lv.Line.Data = geometry; lv.Hit.Data = geometry;
    }

    private void RemoveLinkVis(LinkVis lv)
    {
        _world.Children.Remove(lv.Line); _world.Children.Remove(lv.Hit);
        _links.Remove(lv);
        _wf.Links.Remove(lv.Model);
    }

    /// <summary>Linked dots are indigo; an input nobody feeds is amber (the user is asked for it); a final output is green.</summary>
    private void RefreshPortColors()
    {
        foreach (var nv in _nodes)
        {
            foreach (var (name, dot) in nv.In) dot.Fill = _wf.Links.Any(l => l.ToNode == nv.Model.Id && l.ToPort == name) ? Accent : Amber;
            foreach (var (name, dot) in nv.Out) dot.Fill = _wf.Links.Any(l => l.FromNode == nv.Model.Id && l.FromPort == name) ? Accent : Green;
        }
    }

    // ---------------------------------------------------------------------------------------- whole canvas

    private void Rebuild()
    {
        _world.Children.Clear();
        _nodes.Clear(); _links.Clear();
        _dragFrom = null; _temp = null;
        foreach (var n in _wf.Nodes) { var nv = BuildNodeVis(n); _nodes.Add(nv); _world.Children.Add(nv.Root); }
        foreach (var l in _wf.Links) AddLinkVis(l);
        RefreshPortColors();
        UpdateHint();
    }

    /// <summary>Reads block.json of every block again (after the skills changed) and redraws, keeping places and links.</summary>
    public void RefreshContracts() => Rebuild();

    private void ClearAll()
    {
        if (_wf.Nodes.Count == 0) { Say("The canvas is already empty."); return; }
        if (MessageBox.Show(Window.GetWindow(this), "Remove every block and link from the canvas?\n(A saved .json file is not touched.)", "Clear", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        _wf = new WfFile();
        Rebuild();
        Say("Canvas cleared.");
    }

    private void LoadSample()
    {
        string gu = "Dino-Data_Format-ATE_GU", data = "Dino-Data_Format-ATE_Data", compile = "Dino-Apps-ATE_Compile_Combine";
        var missing = new[] { gu, data, compile }.Where(s => !BlockContract.SkillExists(s)).ToList();
        if (missing.Count > 0) { Say("The sample needs these skills in Storage: " + string.Join(", ", missing)); return; }
        if (_wf.Nodes.Count > 0 && MessageBox.Show(Window.GetWindow(this), "Replace the canvas with the sample?", "Sample", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;

        _wf = new WfFile { Name = "Sample" };
        _wf.Nodes.Add(new WfNode { Id = "n1", Skill = gu, X = 60, Y = 90 });
        _wf.Nodes.Add(new WfNode { Id = "n2", Skill = data, X = 60, Y = 290 });
        _wf.Nodes.Add(new WfNode { Id = "n3", Skill = compile, X = 470, Y = 180 });
        Rebuild();
        var contracts = _nodes.ToDictionary(n => n.Model.Id);
        TryLink(new PortRef(contracts["n1"], "gu_rows", false), new PortRef(contracts["n3"], "benchmark_rows", true));
        TryLink(new PortRef(contracts["n2"], "ate_rows", false), new PortRef(contracts["n3"], "other_rows", true));
        _scale.ScaleX = _scale.ScaleY = 1; _pan.X = _pan.Y = 0; UpdateZoomText();
        Say("Sample loaded: two parsed inputs feed the compile-and-combine skill. Try Run Tests, then Generate Skill.");
    }

    private static string WorkflowFolder()
    {
        var dir = IOPath.Combine(AppPaths.Base, "Workflows");
        try { Directory.CreateDirectory(dir); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return dir;
    }

    private void SaveJson()
    {
        if (_wf.Nodes.Count == 0) { Say("Nothing to save: the canvas is empty."); return; }
        var dlg = new SaveFileDialog
        {
            Title = "Save the wiring", Filter = "Workflow (*.json)|*.json", DefaultExt = ".json", InitialDirectory = WorkflowFolder(),
            FileName = (_wf.Name.Length > 0 ? _wf.Name : "workflow") + ".json",
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            _wf.Name = IOPath.GetFileNameWithoutExtension(dlg.FileName);
            File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(_wf, WorkflowRules.Json), SkillRules.Utf8NoBom);
            Say($"Saved {dlg.FileName}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Say("Save failed: " + ex.Message); }
    }

    private void OpenJson()
    {
        var dlg = new OpenFileDialog { Title = "Open a wiring", Filter = "Workflow (*.json)|*.json|All files (*.*)|*.*", InitialDirectory = WorkflowFolder() };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            var loaded = JsonSerializer.Deserialize<WfFile>(File.ReadAllText(dlg.FileName, Encoding.UTF8), WorkflowRules.Json);
            if (loaded is null || loaded.Nodes.Count == 0) { Say("That file holds no blocks."); return; }
            if (loaded.Nodes.Select(n => n.Id).Distinct().Count() != loaded.Nodes.Count) { Say("That file is not a valid wiring (duplicate block ids)."); return; }
            _wf = loaded;
            if (_wf.Name.Length == 0) _wf.Name = IOPath.GetFileNameWithoutExtension(dlg.FileName);
            _scale.ScaleX = _scale.ScaleY = 1; _pan.X = _pan.Y = 0; UpdateZoomText();
            Rebuild();
            Say($"Opened {IOPath.GetFileName(dlg.FileName)}: {_wf.Nodes.Count} block(s), {_wf.Links.Count} link(s).");
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { Say("Open failed: " + ex.Message); }
    }

    // ---------------------------------------------------------------------------------------- tests and generate

    private List<Finding> FullCheck()
    {
        var findings = WorkflowRules.Validate(_wf);
        if (findings.Any(f => f.Level == "FAIL")) return findings;
        try
        {
            const string testFolder = "Dino-Apps-Workflow_Test";
            var md = WorkflowRules.BuildSkillMd(_wf, testFolder, WorkflowRules.DefaultDescription(_wf));
            var check = SkillRules.Check(md, testFolder);
            foreach (var e in check.Errors) findings.Add(new("FAIL", "Generated skill: " + e));
            foreach (var w in check.Warnings) findings.Add(new("WARN", "Generated skill: " + w));
            if (check.Ok) findings.Add(new("PASS", $"The generated SKILL.md ({md.Count(c => c == '\n')} lines) passes the Dino-Dev-Skill_Format check."));
        }
        catch (Exception ex) { findings.Add(new("FAIL", "The skill could not be generated: " + ex.Message)); }
        return findings;
    }

    private static string Format(List<Finding> findings)
    {
        var sb = new StringBuilder();
        foreach (var level in new[] { "FAIL", "WARN", "PASS", "INFO" })
            foreach (var f in findings.Where(f => f.Level == level)) sb.AppendLine($"[{level}] {f.Message}");
        sb.AppendLine();
        sb.Append($"Result: {findings.Count(f => f.Level == "PASS")} pass, {findings.Count(f => f.Level == "INFO")} info, {findings.Count(f => f.Level == "WARN")} warning(s), {findings.Count(f => f.Level == "FAIL")} fail.");
        return sb.ToString();
    }

    private void RunTests()
    {
        var findings = FullCheck();
        bool ok = !findings.Any(f => f.Level == "FAIL");
        Say(ok ? "Run Tests: all checks passed." : "Run Tests: some checks failed - see the report.");
        new TextViewWindow("Run Tests", ok ? "Wire frame checks: passed" : "Wire frame checks: FAILED", Format(findings), true) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    /// <summary>Turns the wiring into Storage/Dino-Apps-{Name}/SKILL.md (and workflow.json beside it).</summary>
    public void GenerateSkill()
    {
        var owner = Window.GetWindow(this);
        var findings = FullCheck();
        if (findings.Any(f => f.Level == "FAIL"))
        {
            Say("Cannot generate: fix the failed checks first.");
            new TextViewWindow("Generate Skill", "Cannot generate yet", Format(findings), true) { Owner = owner }.ShowDialog();
            return;
        }

        var defaultName = _wf.Name.StartsWith("Dino-Apps-") ? _wf.Name["Dino-Apps-".Length..] : (_wf.Name.Length > 0 && _wf.Name != "Sample" ? _wf.Name : "My_Workflow");
        var answer = WorkflowNameDialog.Ask(owner, defaultName, WorkflowRules.DefaultDescription(_wf));
        if (answer is null) return;
        var (name, description) = answer.Value;

        var folder = "Dino-Apps-" + name;
        var dir = IOPath.Combine(AppPaths.StorageDir, folder);
        var skillPath = IOPath.Combine(dir, SkillStore.SkillFile);
        if (File.Exists(skillPath) && MessageBox.Show(owner, $"{folder} already exists in Storage.\nReplace its SKILL.md and workflow.json?", "Generate Skill", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        try
        {
            var md = WorkflowRules.BuildSkillMd(_wf, folder, description);
            var check = SkillRules.Check(md, folder);
            if (!check.Ok) { Say("Cannot generate: " + string.Join(" ", check.Errors)); return; }

            Directory.CreateDirectory(dir);
            File.WriteAllText(skillPath, check.FixedText, SkillRules.Utf8NoBom);
            _wf.Name = folder;
            File.WriteAllText(IOPath.Combine(dir, "workflow.json"), JsonSerializer.Serialize(_wf, WorkflowRules.Json), SkillRules.Utf8NoBom);
            File.WriteAllText(IOPath.Combine(dir, BlockContract.FileName), WorkflowRules.BuildBlockJson(_wf) + Environment.NewLine, SkillRules.Utf8NoBom); // so this workflow can be a block of another one
            Say($"Generated {folder} with {_wf.Nodes.Count} block(s). Click Sync to copy it to Claude Code, or Upload Skill to put it in the Box zip.");
            Generated?.Invoke();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Say("Generate failed: " + ex.Message); }
    }
}
