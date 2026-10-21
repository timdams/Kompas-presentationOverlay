using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PresentationOverlay;

/// <summary>Eén rij in de sidebar: icoon + label, uitgelicht of gedimd.</summary>
public sealed class ItemView
{
    const double InactiveOpacity = 0.4;
    static readonly Color InactiveBackground = Color.FromRgb(0x1B, 0x1F, 0x29);

    readonly SolidColorBrush _background = new(InactiveBackground);
    readonly ScaleTransform _scale = new(1, 1);
    readonly Image _icon = new() { Width = 56, Height = 56, Stretch = Stretch.Uniform };
    readonly TextBlock _glyph;
    readonly Color _accent;

    public Rule Rule { get; }
    public Border Root { get; }
    public bool HasIcon { get; private set; }

    public ItemView(Rule rule, Color accent)
    {
        Rule = rule;
        _accent = accent;
        RenderOptions.SetBitmapScalingMode(_icon, BitmapScalingMode.HighQuality);

        _glyph = new TextBlock
        {
            Text = rule.Glyph ?? rule.Label[..1].ToUpperInvariant(),
            FontSize = 36,
            FontFamily = new FontFamily("Segoe UI Semibold, Segoe UI Emoji"),
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var iconHost = new Grid { Width = 56, Height = 56 };
        iconHost.Children.Add(_glyph);
        iconHost.Children.Add(_icon);

        var label = new TextBlock
        {
            Text = rule.Label,
            FontSize = 24,
            FontFamily = new FontFamily("Segoe UI Semibold"),
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(iconHost);
        row.Children.Add(label);

        Root = new Border
        {
            Background = _background,
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(10, 6, 10, 6),
            Child = row,
            Opacity = InactiveOpacity,
            RenderTransform = _scale,
            RenderTransformOrigin = new Point(0.5, 0.5),
        };
    }

    public void SetIcon(ImageSource source)
    {
        _icon.Source = source;
        _glyph.Visibility = Visibility.Collapsed;
        HasIcon = true;
    }

    public void SetActive(bool active, bool animate)
    {
        var duration = TimeSpan.FromMilliseconds(animate ? 250 : 0);
        _background.BeginAnimation(SolidColorBrush.ColorProperty,
            new ColorAnimation(active ? _accent : InactiveBackground, duration));
        Root.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(active ? 1.0 : InactiveOpacity, duration));

        if (active && animate)
        {
            var pop = new DoubleAnimation(0.9, 1.0, TimeSpan.FromMilliseconds(450))
            {
                EasingFunction = new BackEase { Amplitude = 0.6, EasingMode = EasingMode.EaseOut },
            };
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        }
    }
}
