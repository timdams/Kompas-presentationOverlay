using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace PresentationOverlay;

/// <summary>
/// Cirkel rond de muisaanwijzer met een ripple bij elke klik, zoals bij screencasts. Een klein,
/// klikdoorlatend venster dat de muis volgt, alleen zichtbaar op het scherm van het publiek.
/// </summary>
public sealed class CursorOverlay : Window
{
    const double Size = 160; // DIP; ruim genoeg voor de ripple
    const double RingSize = 64;
    static readonly Color Tint = Color.FromRgb(0xFF, 0xD4, 0x00);

    readonly Canvas _canvas = new() { Width = Size, Height = Size };
    readonly Ellipse _ring;
    readonly ScaleTransform _ringScale = new(1, 1);
    readonly Native.HookProc _proc; // referentie vasthouden, anders ruimt de GC de callback op

    IntPtr _hwnd, _hook;
    RECT _bounds;
    int _sizePx = (int)Size;
    bool _shown;

    public CursorOverlay()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        Width = Size;
        Height = Size;
        // Buiten beeld starten: het venster verschijnt pas bij de eerste muisbeweging op het juiste scherm.
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -32000;
        Top = -32000;

        _ring = new Ellipse
        {
            Width = RingSize,
            Height = RingSize,
            Fill = new SolidColorBrush(Color.FromArgb(0x40, Tint.R, Tint.G, Tint.B)),
            Stroke = new SolidColorBrush(Color.FromArgb(0xCC, Tint.R, Tint.G, Tint.B)),
            StrokeThickness = 3,
            RenderTransform = _ringScale,
            RenderTransformOrigin = new Point(0.5, 0.5),
        };
        Canvas.SetLeft(_ring, (Size - RingSize) / 2);
        Canvas.SetTop(_ring, (Size - RingSize) / 2);
        _canvas.Children.Add(_ring);
        Content = _canvas;

        _proc = OnMouse;
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            Native.MakeNoActivate(_hwnd);
            Native.MakeClickThrough(_hwnd);
            Hide(_force: true);
        };
    }

    /// <summary>Het scherm waarop de highlight getoond wordt, in fysieke pixels.</summary>
    public void SetTarget(RECT bounds, double scale)
    {
        _bounds = bounds;
        _sizePx = (int)Math.Round(Size * scale);
    }

    public void Start()
    {
        if (_hook != IntPtr.Zero) return;
        _hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _proc, Native.GetModuleHandle(null), 0);
    }

    public void Stop()
    {
        if (_hook != IntPtr.Zero) Native.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        Stop();
        base.OnClosed(e);
    }

    IntPtr OnMouse(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            try
            {
                // MSLLHOOKSTRUCT begint met POINT pt (x, y in schermpixels).
                int x = Marshal.ReadInt32(lParam), y = Marshal.ReadInt32(lParam, 4);
                switch ((int)wParam)
                {
                    case Native.WM_MOUSEMOVE:
                        MoveTo(x, y);
                        break;
                    case Native.WM_LBUTTONDOWN:
                    case Native.WM_RBUTTONDOWN:
                        MoveTo(x, y);
                        Press();
                        break;
                    case Native.WM_LBUTTONUP:
                    case Native.WM_RBUTTONUP:
                        Release();
                        break;
                }
            }
            catch
            {
                // Een fout in de hook mag de muis van de gebruiker nooit blokkeren.
            }
        }
        return Native.CallNextHookEx(_hook, code, wParam, lParam);
    }

    void MoveTo(int x, int y)
    {
        if (_hwnd == IntPtr.Zero) return;
        if (x < _bounds.left || x >= _bounds.right || y < _bounds.top || y >= _bounds.bottom)
        {
            Hide();
            return;
        }

        _shown = true;
        // Topmost bij elke beweging: een fullscreen venster mag ons niet overdekken.
        Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, x - _sizePx / 2, y - _sizePx / 2, _sizePx, _sizePx,
            Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
    }

    void Hide(bool _force = false)
    {
        if (_hwnd == IntPtr.Zero || (!_shown && !_force)) return;
        _shown = false;
        Native.SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
            Native.SWP_HIDEWINDOW | Native.SWP_NOACTIVATE | 0x0001 | 0x0002 | 0x0004); // NOSIZE | NOMOVE | NOZORDER
    }

    void Press()
    {
        if (!_shown) return;
        Animate(_ringScale, 0.75, 90);

        var ripple = new Ellipse
        {
            Width = Size - 8,
            Height = Size - 8,
            Stroke = new SolidColorBrush(Tint),
            StrokeThickness = 4,
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
        };
        var scale = new ScaleTransform(RingSize / Size, RingSize / Size);
        ripple.RenderTransform = scale;
        Canvas.SetLeft(ripple, 4);
        Canvas.SetTop(ripple, 4);
        _canvas.Children.Add(ripple);

        var duration = TimeSpan.FromMilliseconds(500);
        var grow = new DoubleAnimation(1.0, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        var fade = new DoubleAnimation(0.9, 0, duration);
        fade.Completed += (_, _) => _canvas.Children.Remove(ripple);
        ripple.BeginAnimation(OpacityProperty, fade);
    }

    void Release() => Animate(_ringScale, 1.0, 140);

    static void Animate(ScaleTransform scale, double to, int ms)
    {
        var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms));
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }
}
