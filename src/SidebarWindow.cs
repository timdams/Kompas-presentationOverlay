using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace PresentationOverlay;

public sealed class SidebarWindow : Window
{
    readonly string _configPath;
    readonly StackPanel _panel = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly List<ItemView> _items = [];
    readonly UrlReader _urls = new();
    readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(500) };
    readonly DispatcherTimer _reloadDebounce = new() { Interval = TimeSpan.FromMilliseconds(300) };
    readonly uint _ownPid = (uint)Environment.ProcessId;

    FileSystemWatcher? _watcher;
    Native.WinEventProc? _hookProc;
    IntPtr _hook, _hwnd, _targetMonitor;
    Config _fileConfig = new();
    Config _config = new();
    List<Rule> _rules = [];
    OptionsWindow? _options;
    bool _barRegistered;
    int _active = -1;
    int _ticks;

    // Voor het optievenster.
    internal UserState User { get; } = UserState.Load();
    /// <summary>Alleen voor deze sessie: bij de volgende start staan alle tegels er weer.</summary>
    internal HashSet<string> Hidden { get; } = [];
    internal IReadOnlyList<Rule> Rules => _rules;
    internal Config Effective => _config;

    public SidebarWindow(string configPath)
    {
        _configPath = Path.GetFullPath(configPath);

        Title = "Presentation overlay";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x11, 0x15));
        Content = BuildLayout();
        Width = 300;
        Height = 600;

        var menu = new ContextMenu();
        var options = new MenuItem { Header = "Opties…" };
        options.Click += (_, _) => OpenOptions();
        var reload = new MenuItem { Header = "Config herladen" };
        reload.Click += (_, _) => ApplyConfig();
        var exit = new MenuItem { Header = "Afsluiten" };
        exit.Click += (_, _) => Close();
        menu.Items.Add(options);
        menu.Items.Add(reload);
        menu.Items.Add(exit);
        ContextMenu = menu;

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            Native.MakeNoActivate(_hwnd);
            ApplyConfig();
            StartWatching();
        };

        _tick.Tick += (_, _) => OnTick();
        _reloadDebounce.Tick += (_, _) => { _reloadDebounce.Stop(); ApplyConfig(); };
        _urls.Changed += Evaluate;
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Dispatcher.BeginInvoke(PositionAppBar);
    }

    protected override void OnClosed(EventArgs e)
    {
        _tick.Stop();
        _watcher?.Dispose();
        if (_hook != IntPtr.Zero) Native.UnhookWinEvent(_hook);
        RemoveAppBar();
        base.OnClosed(e);
    }

    Grid BuildLayout()
    {
        var gear = new TextBlock
        {
            Text = "", // tandwiel in Segoe MDL2 Assets
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 18,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var button = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(20),
            Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x1F, 0x29)),
            Margin = new Thickness(14),
            Opacity = 0.35,
            Child = gear,
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            ToolTip = "Opties",
        };
        button.MouseEnter += (_, _) => button.Opacity = 1;
        button.MouseLeave += (_, _) => button.Opacity = 0.35;
        button.MouseLeftButtonUp += (_, _) => OpenOptions();

        var grid = new Grid();
        grid.Children.Add(_panel);
        grid.Children.Add(button);
        return grid;
    }

    void OpenOptions()
    {
        if (_options != null) { _options.Activate(); return; }
        _options = new OptionsWindow(this);
        _options.Closed += (_, _) => _options = null;
        _options.Show();
    }

    void ApplyConfig()
    {
        try
        {
            _fileConfig = Config.Load(_configPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Config laden mislukt: {ex.Message}");
            if (_rules.Count > 0) return; // behoud de vorige werkende config
        }
        ApplyEffective(reposition: true);
    }

    /// <summary>Combineert config.json met wat de gebruiker in de app instelde.</summary>
    internal void ApplyEffective(bool reposition)
    {
        _config = new Config
        {
            Monitor = User.Monitor ?? _fileConfig.Monitor,
            Width = User.Width ?? _fileConfig.Width,
            FitFullscreen = User.FitFullscreen ?? _fileConfig.FitFullscreen,
            Accent = User.Accent ?? _fileConfig.Accent,
            Items = _fileConfig.Items,
        };
        // OrderBy is stabiel: tegels zonder bewaarde plek blijven in config-volgorde achteraan.
        _rules = _fileConfig.Items.Concat(User.Extras)
            .OrderBy(r => { var i = User.Order.IndexOf(r.Label); return i < 0 ? int.MaxValue : i; })
            .ToList();

        Rebuild();
        if (reposition || _targetMonitor == IntPtr.Zero) PositionAppBar();

        if (_hook == IntPtr.Zero)
        {
            _hookProc = (_, _, _, _, _, _, _) => Evaluate();
            _hook = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _hookProc, 0, 0, Native.WINEVENT_OUTOFCONTEXT);
            _tick.Start();
        }
        Evaluate();
    }

    /// <summary>Bouwt de tegels opnieuw op uit de regels, minus de verborgen; de highlight blijft staan.</summary>
    internal void Rebuild()
    {
        var activeLabel = _active >= 0 && _active < _items.Count ? _items[_active].Rule.Label : null;
        var accent = (Color)ColorConverter.ConvertFromString(_config.Accent);

        _panel.Children.Clear();
        _items.Clear();
        foreach (var rule in _rules.Where(r => !Hidden.Contains(r.Label)))
        {
            var item = new ItemView(rule, accent);
            WireTile(item);
            _items.Add(item);
            _panel.Children.Add(item.Root);
        }
        _active = -1;

        ResolveIcons();
        if (activeLabel != null)
        {
            var index = _items.FindIndex(i => i.Rule.Label == activeLabel);
            if (index >= 0) SetActive(index);
        }
    }

    void WireTile(ItemView item)
    {
        var root = item.Root;

        var hide = new MenuItem { Header = "Verberg" };
        hide.Click += (_, _) => { Hidden.Add(item.Rule.Label); Rebuild(); };
        root.ContextMenu = new ContextMenu { Items = { hide } };

        // Slepen om te herordenen: de tegel schuift mee zodra de muis het midden van een buur passeert.
        Point start = default;
        bool down = false, dragging = false;
        root.MouseLeftButtonDown += (_, e) =>
        {
            start = e.GetPosition(_panel);
            down = true;
            dragging = false;
            root.CaptureMouse();
        };
        root.MouseMove += (_, e) =>
        {
            if (!down) return;
            var p = e.GetPosition(_panel);
            if (!dragging)
            {
                if (Math.Abs(p.Y - start.Y) < 6) return;
                dragging = true;
                root.Cursor = System.Windows.Input.Cursors.SizeNS;
            }

            var cur = _panel.Children.IndexOf(root);
            var target = cur;
            if (cur > 0 && p.Y < CenterY((FrameworkElement)_panel.Children[cur - 1])) target = cur - 1;
            else if (cur < _panel.Children.Count - 1 && p.Y > CenterY((FrameworkElement)_panel.Children[cur + 1])) target = cur + 1;
            if (target == cur) return;

            _panel.Children.RemoveAt(cur);
            _panel.Children.Insert(target, root);
            (_items[cur], _items[target]) = (_items[target], _items[cur]);
            if (_active == cur) _active = target;
            else if (_active == target) _active = cur;
            _panel.UpdateLayout();
        };
        root.MouseLeftButtonUp += (_, _) =>
        {
            if (!down) return;
            down = false;
            root.ReleaseMouseCapture();
            root.ClearValue(FrameworkElement.CursorProperty);
            if (dragging) SaveOrder();
            dragging = false;
        };
    }

    double CenterY(FrameworkElement element) =>
        element.TranslatePoint(new Point(0, 0), _panel).Y + element.ActualHeight / 2;

    /// <summary>Verborgen tegels houden hun plek; de zichtbare worden in de vrije plekken herschikt.</summary>
    void SaveOrder()
    {
        var visible = new Queue<Rule>(_items.Select(i => i.Rule));
        _rules = _rules.Select(r => Hidden.Contains(r.Label) ? r : visible.Dequeue()).ToList();
        User.Order = _rules.Select(r => r.Label).ToList();
        User.Save();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _options?.Close();
        base.OnClosing(e);
    }

    void StartWatching()
    {
        var dir = Path.GetDirectoryName(_configPath)!;
        _watcher = new FileSystemWatcher(dir, Path.GetFileName(_configPath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        FileSystemEventHandler onChange = (_, _) => Dispatcher.BeginInvoke(() =>
        {
            _reloadDebounce.Stop();
            _reloadDebounce.Start();
        });
        _watcher.Changed += onChange;
        _watcher.Created += onChange;
        _watcher.Renamed += (s, e) => onChange(s, e);
    }

    void OnTick()
    {
        // Iconen van apps die pas na de start opduiken alsnog ophalen; titel-/url-wissels opvangen.
        if (++_ticks % 6 == 0) ResolveIcons();
        Evaluate();
    }

    void ResolveIcons()
    {
        foreach (var item in _items.Where(i => !i.HasIcon))
        {
            var rule = item.Rule;
            var candidates = new List<string?> { rule.Icon, rule.Exe };
            foreach (var name in rule.Process)
            {
                try
                {
                    var proc = Process.GetProcessesByName(Rule.Strip(name)).FirstOrDefault();
                    if (proc != null) candidates.Add(Native.ProcessPath((uint)proc.Id));
                }
                catch { /* proces verdwenen */ }
            }

            foreach (var path in candidates)
            {
                if (string.IsNullOrEmpty(path)) continue;
                var icon = Native.LoadIcon(Environment.ExpandEnvironmentVariables(path));
                if (icon == null) continue;
                item.SetIcon(icon);
                break;
            }
        }
    }

    void Evaluate()
    {
        var hwnd = Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || _items.Count == 0) return;

        Native.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == _ownPid) return;

        // Alleen het scherm van het publiek telt; de speaker view op het andere scherm niet.
        if (Native.MonitorFromWindow(hwnd, 2 /* NEAREST */) != _targetMonitor) return;

        var path = Native.ProcessPath(pid);
        if (path == null) return;
        var process = Path.GetFileNameWithoutExtension(path);
        var className = Native.WindowClass(hwnd);
        var title = Native.WindowText(hwnd);

        for (var i = 0; i < _items.Count; i++)
        {
            var rule = _items[i].Rule;
            if (!rule.Matches(process, className, title, () => _urls.Get(hwnd, title))) continue;

            if (!_items[i].HasIcon)
            {
                var icon = Native.LoadIcon(path);
                if (icon != null) _items[i].SetIcon(icon);
            }
            SetActive(i);
            if (_config.FitFullscreen) FitBesideSidebar(hwnd);
            // Topmost opnieuw afdwingen: een fullscreen venster kan ons anders overdekken.
            Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0, 0x0001 | 0x0002 | Native.SWP_NOACTIVATE);
            return;
        }
        // Geen match: highlight blijft waar hij was.
    }

    /// <summary>
    /// Een fullscreen venster (F11) dekt de hele monitor en dus ook onze sidebar. Zodra een
    /// herkend venster exact de monitor vult, schuiven we de linkerrand op naar de sidebar.
    /// </summary>
    void FitBesideSidebar(IntPtr hwnd)
    {
        var monitor = Native.Monitors().FirstOrDefault(m => m.Handle == _targetMonitor);
        if (monitor == null || !Native.GetWindowRect(hwnd, out var r) || !Native.GetWindowRect(_hwnd, out var bar)) return;

        var b = monitor.Bounds;
        if (r.left != b.left || r.top != b.top || r.right != b.right || r.bottom != b.bottom) return;

        Native.SetWindowPos(hwnd, IntPtr.Zero, bar.right, b.top, b.right - bar.right, b.bottom - b.top,
            0x0004 /* NOZORDER */ | Native.SWP_NOACTIVATE);
    }

    void SetActive(int index)
    {
        if (index == _active) return;
        for (var i = 0; i < _items.Count; i++)
            _items[i].SetActive(i == index, animate: _active != -1 || i == index);
        _active = index;
    }

    MonitorInfo PickMonitor()
    {
        var all = Native.Monitors();
        var primary = all.First(m => m.Primary);
        return _config.Monitor.ToLowerInvariant() switch
        {
            "primary" => primary,
            "secondary" => all.FirstOrDefault(m => !m.Primary) ?? primary,
            var s when int.TryParse(s, out var idx) && idx >= 0 && idx < all.Count => all[idx],
            _ => all.FirstOrDefault(m => !m.Primary) ?? primary,
        };
    }

    /// <summary>Registreert als AppBar (zoals de taakbalk): Windows houdt de strook vrij voor andere vensters.</summary>
    void PositionAppBar()
    {
        if (_hwnd == IntPtr.Zero) return;
        if (_barRegistered) RemoveAppBar();

        var monitor = PickMonitor();
        _targetMonitor = monitor.Handle;
        var widthPx = (int)Math.Round(_config.Width * Native.MonitorScale(monitor.Handle));

        var abd = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>(), hWnd = _hwnd };
        Native.SHAppBarMessage(Native.ABM_NEW, ref abd);
        _barRegistered = true;

        abd.uEdge = Native.ABE_LEFT;
        abd.rc = new RECT
        {
            left = monitor.Bounds.left,
            top = monitor.Work.top,
            right = monitor.Bounds.left + widthPx,
            bottom = monitor.Work.bottom,
        };
        Native.SHAppBarMessage(Native.ABM_QUERYPOS, ref abd);
        abd.rc.right = abd.rc.left + widthPx;
        Native.SHAppBarMessage(Native.ABM_SETPOS, ref abd);

        Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, abd.rc.left, abd.rc.top, widthPx,
            abd.rc.bottom - abd.rc.top, Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
    }

    void RemoveAppBar()
    {
        if (!_barRegistered) return;
        var abd = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>(), hWnd = _hwnd };
        Native.SHAppBarMessage(Native.ABM_REMOVE, ref abd);
        _barRegistered = false;
    }
}
