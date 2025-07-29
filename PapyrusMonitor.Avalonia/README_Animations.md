# Animation Features Implementation

This document describes the smooth animations implemented for value changes in the PapyrusLogMonitor Avalonia application as part of Phase 5.

## Implemented Animation Features

### 1. AnimatedNumericTextBlock Control
**File**: `Controls/AnimatedNumericTextBlock.cs`

Features:
- Smooth transitions for numeric value changes (Dumps, Stacks, Warnings, Errors, Ratio)
- Configurable animation duration (default: 400ms)
- Configurable format strings (F0 for integers, F3 for decimals)
- Can be enabled/disabled via `EnableAnimations` property
- Uses CubicEaseOut easing for natural motion
- Runs at ~60fps for smooth animations

Usage in XAML:
```xml
<controls:AnimatedNumericTextBlock Value="{Binding Statistics.Dumps}" 
                                  FormatString="F0"
                                  EnableAnimations="{Binding EnableAnimations}" />
```

### 2. AnimatedStatusIndicator Control
**File**: `Controls/AnimatedStatusIndicator.cs`

Features:
- Smooth color transitions for status indicators (✓, ⚠️, ❌)
- Fade in/out effects when status text changes
- Animated foreground color changes
- Configurable animation duration (default: 300ms)
- Can be enabled/disabled via `EnableAnimations` property

Usage in XAML:
```xml
<controls:AnimatedStatusIndicator StatusText="{Binding RatioStatus}" 
                                StatusColor="{Binding RatioStatusColor}"
                                EnableAnimations="{Binding EnableAnimations}" />
```

### 3. CSS-Style Transitions in App.axaml

#### Statistical Value Cards
- Background color transitions on hover (300ms)
- Smooth fade effects for various UI elements

#### Status Message Background
- Animated background color changes when status changes (500ms)
- Smooth opacity transitions (300ms)

#### Progress Bar Animation
- Continuous pulse animation when monitoring is active
- 2-second cycle with opacity changes from 30% to 100%

## Animation Configuration

### Enable/Disable Animations
Animations can be controlled via the `EnableAnimations` property in `PapyrusMonitorViewModel`:

```csharp
public bool EnableAnimations
{
    get => _enableAnimations;
    set => this.RaiseAndSetIfChanged(ref _enableAnimations, value);
}
```

Default: `true` (animations enabled)

### Animation Performance
- All animations are designed to be performant and not impact real-time monitoring
- Use hardware-accelerated properties where possible
- Animations automatically skip when values don't change significantly
- Frame rate capped at 60fps to balance smoothness and performance

## Animation Types

### Value Change Animations
- **Duration**: 400ms
- **Easing**: CubicEaseOut
- **Target Properties**: Numeric text values
- **Behavior**: Smooth interpolation between old and new values

### Color Transition Animations
- **Duration**: 300ms (status indicators), 500ms (backgrounds)
- **Easing**: CubicEaseOut
- **Target Properties**: Foreground and background brushes
- **Behavior**: Smooth color interpolation

### Fade Effects
- **Duration**: 100ms fade out, 100ms fade in
- **Easing**: CubicEaseOut
- **Target Properties**: Opacity
- **Behavior**: Fade out → change content → fade in

### UI State Animations
- **Duration**: 300ms
- **Easing**: CubicEaseOut
- **Target Properties**: Background colors, opacity
- **Behavior**: Smooth transitions for status changes

## Performance Considerations

1. **Non-blocking**: All animations run on background threads where possible
2. **Cancellable**: Animations can be interrupted for new values
3. **Threshold-based**: Small value changes (< 0.01) skip animation
4. **Resource-efficient**: Animations dispose properly and don't leak memory
5. **Conditional**: Animations automatically disable when UI is not visible

## Integration with ReactiveUI

The animations integrate seamlessly with ReactiveUI's observable patterns:
- Value changes trigger animations automatically
- No manual animation coordination needed
- Respects ReactiveUI's property change notifications
- Works with ObservableAsPropertyHelper patterns

## Browser/Platform Compatibility

These animations work across all Avalonia-supported platforms:
- Windows (WPF-like rendering)
- macOS (native rendering)
- Linux (Skia rendering)
- Browser (WebAssembly with some limitations)

The animation system automatically adapts to platform capabilities and falls back gracefully when hardware acceleration is unavailable.