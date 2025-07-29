using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace PapyrusMonitor.Avalonia.Controls;

/// <summary>
/// A TextBlock that animates numeric value changes with smooth transitions
/// </summary>
public class AnimatedNumericTextBlock : TextBlock
{
    private double _currentValue;
    private double _targetValue;
    private readonly Animation _valueAnimation;

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<AnimatedNumericTextBlock, double>(
            nameof(Value), 
            defaultValue: 0,
            coerce: (_, value) => value);

    public static readonly StyledProperty<string> FormatStringProperty =
        AvaloniaProperty.Register<AnimatedNumericTextBlock, string>(
            nameof(FormatString), 
            defaultValue: "F0");

    public static readonly StyledProperty<TimeSpan> AnimationDurationProperty =
        AvaloniaProperty.Register<AnimatedNumericTextBlock, TimeSpan>(
            nameof(AnimationDuration), 
            defaultValue: TimeSpan.FromMilliseconds(400));

    public static readonly StyledProperty<bool> EnableAnimationsProperty =
        AvaloniaProperty.Register<AnimatedNumericTextBlock, bool>(
            nameof(EnableAnimations), 
            defaultValue: true);

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string FormatString
    {
        get => GetValue(FormatStringProperty);
        set => SetValue(FormatStringProperty, value);
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

    static AnimatedNumericTextBlock()
    {
        ValueProperty.Changed.AddClassHandler<AnimatedNumericTextBlock>((x, e) => x.OnValueChanged(e));
        FormatStringProperty.Changed.AddClassHandler<AnimatedNumericTextBlock>((x, e) => x.UpdateText());
    }

    public AnimatedNumericTextBlock()
    {
        _valueAnimation = new Animation
        {
            Duration = AnimationDuration,
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Forward
        };
    }

    private void OnValueChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var newValue = (double)e.NewValue!;
        
        if (!EnableAnimations || !IsVisible)
        {
            _currentValue = newValue;
            _targetValue = newValue;
            UpdateText();
            return;
        }

        _targetValue = newValue;
        AnimateValue();
    }

    private async void AnimateValue()
    {
        var startValue = _currentValue;
        var endValue = _targetValue;
        var duration = AnimationDuration;

        if (Math.Abs(startValue - endValue) < 0.01)
        {
            _currentValue = endValue;
            UpdateText();
            return;
        }

        var startTime = DateTime.Now;
        var easingFunction = new CubicEaseOut();

        while (Math.Abs(_currentValue - endValue) > 0.01)
        {
            var elapsed = DateTime.Now - startTime;
            var progress = Math.Min(1.0, elapsed.TotalMilliseconds / duration.TotalMilliseconds);
            var easedProgress = easingFunction.Ease(progress);

            _currentValue = startValue + (endValue - startValue) * easedProgress;
            UpdateText();

            if (progress >= 1.0)
            {
                _currentValue = endValue;
                UpdateText();
                break;
            }

            await System.Threading.Tasks.Task.Delay(16); // ~60fps
        }
    }

    private void UpdateText()
    {
        if (FormatString.StartsWith("F"))
        {
            Text = _currentValue.ToString(FormatString);
        }
        else
        {
            Text = ((int)Math.Round(_currentValue)).ToString(FormatString);
        }
    }
}