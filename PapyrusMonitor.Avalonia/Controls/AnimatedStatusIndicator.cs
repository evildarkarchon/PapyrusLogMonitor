using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace PapyrusMonitor.Avalonia.Controls;

/// <summary>
///     A status indicator control that animates color and text changes
/// </summary>
public class AnimatedStatusIndicator : ContentControl
{
    public static readonly StyledProperty<string> StatusTextProperty =
        AvaloniaProperty.Register<AnimatedStatusIndicator, string>(
            nameof(StatusText),
            "✓");

    public static readonly StyledProperty<IBrush> StatusColorProperty =
        AvaloniaProperty.Register<AnimatedStatusIndicator, IBrush>(
            nameof(StatusColor),
            Brushes.Green);

    public static readonly StyledProperty<TimeSpan> AnimationDurationProperty =
        AvaloniaProperty.Register<AnimatedStatusIndicator, TimeSpan>(
            nameof(AnimationDuration),
            TimeSpan.FromMilliseconds(300));

    public static readonly StyledProperty<bool> EnableAnimationsProperty =
        AvaloniaProperty.Register<AnimatedStatusIndicator, bool>(
            nameof(EnableAnimations),
            true);

    private readonly BrushTransition _foregroundTransition;
    private readonly DoubleTransition _opacityTransition;
    private TextBlock? _textBlock;

    static AnimatedStatusIndicator()
    {
        StatusTextProperty.Changed.AddClassHandler<AnimatedStatusIndicator>((x, e) => x.OnStatusTextChanged(e));
        StatusColorProperty.Changed.AddClassHandler<AnimatedStatusIndicator>((x, e) => x.OnStatusColorChanged(e));
    }

    public AnimatedStatusIndicator()
    {
        _opacityTransition = new DoubleTransition
        {
            Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(150), Easing = new CubicEaseOut()
        };

        _foregroundTransition = new BrushTransition
        {
            Property = ForegroundProperty, Duration = AnimationDuration, Easing = new CubicEaseOut()
        };

        FontSize = 16;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        
        // Initialize content immediately
        InitializeContent();
    }

    public string StatusText
    {
        get => GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public IBrush StatusColor
    {
        get => GetValue(StatusColorProperty);
        set => SetValue(StatusColorProperty, value);
    }

    public TimeSpan AnimationDuration
    {
        get => GetValue(AnimationDurationProperty);
        set => SetValue(AnimationDurationProperty, value);
    }

    public bool EnableAnimations
    {
        get => GetValue(EnableAnimationsProperty);
        set => SetValue(EnableAnimationsProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        InitializeContent();
    }
    
    /// <summary>
    /// Public method for tests to initialize the control
    /// </summary>
    public new void ApplyTemplate()
    {
        InitializeContent();
    }
    
    private void InitializeContent()
    {
        _textBlock = new TextBlock
        {
            Text = StatusText,
            FontSize = FontSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = StatusColor
        };

        if (EnableAnimations)
        {
            _textBlock.Transitions = [_foregroundTransition];
        }

        Content = _textBlock;
    }

    private async void OnStatusTextChanged(AvaloniaPropertyChangedEventArgs e)
    {
        try
        {
            if (_textBlock == null)
            {
                return;
            }

            var oldText = (string)e.OldValue!;
            var newText = (string)e.NewValue!;

            if (!EnableAnimations || string.IsNullOrEmpty(oldText))
            {
                _textBlock.Text = newText;
                return;
            }

            // Fade out
            _textBlock.Opacity = 1;
            await AnimateOpacity(0, TimeSpan.FromMilliseconds(100));

            // Change text
            _textBlock.Text = newText;

            // Fade in
            await AnimateOpacity(1, TimeSpan.FromMilliseconds(100));
        }
        catch (Exception)
        {
            // Swallow exceptions to prevent process crash
            // Animation failure is non-critical
        }
    }

    private void OnStatusColorChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (_textBlock == null)
        {
            return;
        }

        var newColor = (IBrush)e.NewValue!;

        if (!EnableAnimations)
        {
            _textBlock.Foreground = newColor;
            return;
        }

        // The transition will handle the animation
        _textBlock.Foreground = newColor;
    }

    private async Task AnimateOpacity(double targetOpacity, TimeSpan duration)
    {
        if (_textBlock == null)
        {
            return;
        }

        var startOpacity = _textBlock.Opacity;
        var startTime = DateTime.Now;
        var easing = new CubicEaseOut();

        while (true)
        {
            var elapsed = DateTime.Now - startTime;
            var progress = Math.Min(1.0, elapsed.TotalMilliseconds / duration.TotalMilliseconds);
            var easedProgress = easing.Ease(progress);

            _textBlock.Opacity = startOpacity + (targetOpacity - startOpacity) * easedProgress;

            if (progress >= 1.0)
            {
                _textBlock.Opacity = targetOpacity;
                break;
            }

            await Task.Delay(16); // ~60fps
        }
    }
}
