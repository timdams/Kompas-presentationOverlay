using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PresentationOverlay;

/// <summary>
/// Opties voor de presentator. Een gewoon venster (de sidebar zelf kan geen toetsenbord
/// ontvangen) dat op het hoofdscherm opent. Alle controls zijn eigen "chips" zodat het
/// donkere thema klopt zonder WPF-templates te hoeven overschrijven.
/// </summary>
public sealed class OptionsWindow : Window
{
    static readonly Color Bg = Color.FromRgb(0x0F, 0x11, 0x15);
    static readonly Color Card = Color.FromRgb(0x1B, 0x1F, 0x29);
    static readonly Color CardHover = Color.FromRgb(0x26, 0x2B, 0x38);
    static readonly string[] Accents = ["#E4002B", "#F28C28", "#2EA043", "#1F6FEB", "#8957E5", "#14B8A6"];

    readonly SidebarWindow _host;
    readonly StackPanel _body = new() { Margin = new Thickness(24, 0, 24, 24) };

    // Invoer voor "extra app"; blijft bestaan als het venster opnieuw getekend wordt.
    string _newLabel = "", _newProcess = "", _newExe = "", _status = "";

    public OptionsWindow(SidebarWindow host)
    {
        _host = host;
        Title = "Presentation overlay – opties";
        Width = 480;
        Height = 680;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Bg);
        Foreground = Brushes.White;
        FontFamily = new FontFamily("Segoe UI");
        BorderBrush = new SolidColorBrush(Card);
        BorderThickness = new Thickness(1);

        var title = new TextBlock
        {
            Text = "Opties",
            FontSize = 22,
            FontFamily = new FontFamily("Segoe UI Semibold"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var close = Chip("Sluiten", false, Close);
        var header = new DockPanel { Margin = new Thickness(24, 18, 24, 12), Background = Brushes.Transparent };
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        header.Children.Add(title);
        header.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _body,
        });
        Content = root;

        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Render();
    }

    // ---- inhoud ----

    void Render()
    {
        _body.Children.Clear();
        RenderHidden();
        RenderAdd();
        RenderExtras();
        RenderDisplay();
    }

    void RenderHidden()
    {
        Section("Verborgen tegels");
        var hidden = _host.Rules.Where(r => _host.Hidden.Contains(r.Label)).ToList();
        if (hidden.Count == 0)
        {
            _body.Children.Add(Muted("Niets verborgen. Rechtsklik op een tegel om hem te verbergen."));
            return;
        }
        var wrap = new WrapPanel();
        foreach (var rule in hidden)
        {
            var rule1 = rule;
            wrap.Children.Add(Chip($"+  {rule.Label}", false, () =>
            {
                _host.Hidden.Remove(rule1.Label);
                _host.Rebuild();
                Render();
            }));
        }
        _body.Children.Add(wrap);
    }

    void RenderAdd()
    {
        Section("Extra app toevoegen");
        _body.Children.Add(Muted("Kies een draaiende app, of blader naar een .exe."));

        var list = new StackPanel();
        foreach (var app in RunningApps())
        {
            var selected = string.Equals(_newProcess, app.Process + ".exe", StringComparison.OrdinalIgnoreCase);
            var a = app;
            list.Children.Add(Row($"{app.Process}", app.Title, selected, () =>
            {
                _newProcess = a.Process + ".exe";
                _newExe = a.Path ?? "";
                if (string.IsNullOrWhiteSpace(_newLabel)) _newLabel = Capitalize(a.Process);
                _status = "";
                Render();
            }));
        }
        _body.Children.Add(new ScrollViewer
        {
            MaxHeight = 170,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = list,
            Margin = new Thickness(0, 4, 0, 8),
        });

        var browse = Chip("Bladeren naar .exe…", false, () =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Programma's (*.exe)|*.exe", Title = "Kies een programma" };
            if (dlg.ShowDialog(this) != true) return;
            _newProcess = Path.GetFileName(dlg.FileName);
            _newExe = dlg.FileName;
            if (string.IsNullOrWhiteSpace(_newLabel)) _newLabel = Capitalize(Path.GetFileNameWithoutExtension(dlg.FileName));
            _status = "";
            Render();
        });
        _body.Children.Add(browse);

        _body.Children.Add(Muted(_newProcess == "" ? "Geen app gekozen" : $"Gekozen: {_newProcess}"));

        var name = new TextBox
        {
            Text = _newLabel,
            FontSize = 16,
            Padding = new Thickness(10, 7, 10, 7),
            Background = new SolidColorBrush(Card),
            Foreground = Brushes.White,
            CaretBrush = Brushes.White,
            BorderBrush = new SolidColorBrush(CardHover),
            Margin = new Thickness(0, 4, 0, 8),
        };
        name.TextChanged += (_, _) => _newLabel = name.Text;
        _body.Children.Add(Muted("Naam op de tegel"));
        _body.Children.Add(name);

        _body.Children.Add(Chip("Toevoegen", true, AddExtra));
        if (_status != "")
            _body.Children.Add(new TextBlock { Text = _status, Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x8A)), Margin = new Thickness(0, 6, 0, 0) });
    }

    void AddExtra()
    {
        var label = _newLabel.Trim();
        if (_newProcess == "") { _status = "Kies eerst een app."; Render(); return; }
        if (label == "") { _status = "Geef de tegel een naam."; Render(); return; }
        if (_host.Rules.Any(r => string.Equals(r.Label, label, StringComparison.OrdinalIgnoreCase)))
        {
            _status = "Die naam bestaat al.";
            Render();
            return;
        }

        _host.User.Extras.Add(new Rule
        {
            Label = label,
            Process = [_newProcess],
            Exe = _newExe == "" ? null : _newExe,
        });
        _host.User.Save();
        _newLabel = _newProcess = _newExe = _status = "";
        _host.ApplyEffective(reposition: false);
        Render();
    }

    void RenderExtras()
    {
        if (_host.User.Extras.Count == 0) return;
        Section("Eigen tegels");
        var wrap = new WrapPanel();
        foreach (var rule in _host.User.Extras.ToList())
        {
            var r = rule;
            wrap.Children.Add(Chip($"✕  {rule.Label}", false, () =>
            {
                _host.User.Extras.Remove(r);
                _host.User.Order.Remove(r.Label);
                _host.Hidden.Remove(r.Label);
                _host.User.Save();
                _host.ApplyEffective(reposition: false);
                Render();
            }));
        }
        _body.Children.Add(wrap);
    }

    void RenderDisplay()
    {
        var user = _host.User;
        var eff = _host.Effective;

        Section("Weergave");

        _body.Children.Add(Muted("Scherm"));
        var monitors = new WrapPanel();
        var choices = new List<(string Value, string Text)> { ("secondary", "Tweede scherm"), ("primary", "Hoofdscherm") };
        var count = Native.Monitors().Count;
        if (count > 2) for (var i = 0; i < count; i++) choices.Add((i.ToString(), $"Scherm {i + 1}"));
        foreach (var (value, text) in choices)
        {
            var v = value;
            monitors.Children.Add(Chip(text, string.Equals(eff.Monitor, value, StringComparison.OrdinalIgnoreCase), () =>
            {
                user.Monitor = v;
                Commit(reposition: true);
            }));
        }
        _body.Children.Add(monitors);

        _body.Children.Add(Muted("Breedte van de sidebar"));
        var width = new WrapPanel();
        width.Children.Add(Chip("−", false, () => { user.Width = Math.Max(160, eff.Width - 20); Commit(true); }));
        width.Children.Add(new TextBlock
        {
            Text = eff.Width.ToString(),
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 12, 0),
        });
        width.Children.Add(Chip("+", false, () => { user.Width = Math.Min(600, eff.Width + 20); Commit(true); }));
        _body.Children.Add(width);

        _body.Children.Add(Muted("Fullscreen-vensters"));
        _body.Children.Add(Chip(eff.FitFullscreen ? "Naast de sidebar passen: aan" : "Naast de sidebar passen: uit",
            eff.FitFullscreen, () => { user.FitFullscreen = !eff.FitFullscreen; Commit(false); }));

        _body.Children.Add(Muted("Accentkleur"));
        var accents = new WrapPanel();
        foreach (var hex in Accents)
        {
            var h = hex;
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var selected = string.Equals(eff.Accent, hex, StringComparison.OrdinalIgnoreCase);
            var dot = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(15),
                Background = new SolidColorBrush(color),
                BorderBrush = selected ? Brushes.White : Brushes.Transparent,
                BorderThickness = new Thickness(2),
                Margin = new Thickness(0, 4, 10, 4),
                Cursor = Cursors.Hand,
            };
            dot.MouseLeftButtonUp += (_, _) => { user.Accent = h; Commit(false); };
            accents.Children.Add(dot);
        }
        _body.Children.Add(accents);
    }

    void Commit(bool reposition)
    {
        _host.User.Save();
        _host.ApplyEffective(reposition);
        Render();
    }

    // ---- bouwstenen ----

    void Section(string text) => _body.Children.Add(new TextBlock
    {
        Text = text,
        FontSize = 17,
        FontFamily = new FontFamily("Segoe UI Semibold"),
        Margin = new Thickness(0, 22, 0, 6),
    });

    static TextBlock Muted(string text) => new()
    {
        Text = text,
        FontSize = 13,
        Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0xA0, 0xAE)),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 6, 0, 4),
    };

    FrameworkElement Chip(string text, bool selected, Action onClick)
    {
        var brush = new SolidColorBrush(selected ? Color.FromRgb(0x26, 0x2B, 0x38) : Card);
        var chip = new Border
        {
            Background = brush,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16, 8, 16, 8),
            Margin = new Thickness(0, 4, 8, 4),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left,
            BorderThickness = new Thickness(1.5),
            BorderBrush = selected ? new SolidColorBrush(Color.FromRgb(0xE4, 0x00, 0x2B)) : Brushes.Transparent,
            Child = new TextBlock { Text = text, FontSize = 15, Foreground = Brushes.White },
        };
        chip.MouseEnter += (_, _) => brush.Color = CardHover;
        chip.MouseLeave += (_, _) => brush.Color = selected ? CardHover : Card;
        // Niet laten doorborrelen: de kop start anders DragMove en die slokt de muis-omhoog op.
        chip.MouseLeftButtonDown += (_, e) => e.Handled = true;
        chip.MouseLeftButtonUp += (_, _) => onClick();
        return chip;
    }

    FrameworkElement Row(string main, string sub, bool selected, Action onClick)
    {
        var brush = new SolidColorBrush(selected ? CardHover : Card);
        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        stack.Children.Add(new TextBlock { Text = main, FontSize = 15, FontFamily = new FontFamily("Segoe UI Semibold"), Margin = new Thickness(0, 0, 10, 0) });
        stack.Children.Add(new TextBlock
        {
            Text = sub,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0xA0, 0xAE)),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 250,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var row = new Border
        {
            Background = brush,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 2, 0, 2),
            Cursor = Cursors.Hand,
            Child = stack,
        };
        row.MouseEnter += (_, _) => brush.Color = CardHover;
        row.MouseLeave += (_, _) => brush.Color = selected ? CardHover : Card;
        row.MouseLeftButtonUp += (_, _) => onClick();
        return row;
    }

    static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    record RunningApp(string Process, string Title, string? Path);

    static List<RunningApp> RunningApps()
    {
        var own = Environment.ProcessId;
        var apps = new Dictionary<string, RunningApp>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.Id == own || p.MainWindowHandle == IntPtr.Zero || p.MainWindowTitle == "") continue;
                if (apps.ContainsKey(p.ProcessName)) continue;
                apps[p.ProcessName] = new RunningApp(p.ProcessName, p.MainWindowTitle, Native.ProcessPath((uint)p.Id));
            }
            catch { /* proces verdwenen of niet leesbaar */ }
            finally { p.Dispose(); }
        }
        return apps.Values.OrderBy(a => a.Process, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
