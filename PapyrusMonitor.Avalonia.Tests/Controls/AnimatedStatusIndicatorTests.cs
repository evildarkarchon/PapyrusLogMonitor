using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using PapyrusMonitor.Avalonia.Controls;

namespace PapyrusMonitor.Avalonia.Tests.Controls;

[Collection("AvaloniaUITests")]
public class AnimatedStatusIndicatorTests
{
    [AvaloniaFact]
    public void Constructor_Should_Initialize_Default_Properties()
    {
        // Arrange & Act
        var indicator = new AnimatedStatusIndicator();

        // Assert
        indicator.StatusText.Should().Be("✓");
        indicator.StatusColor.Should().NotBeNull();
        // Avalonia may use ImmutableSolidColorBrush instead of SolidColorBrush
        indicator.StatusColor.Should().BeAssignableTo<ISolidColorBrush>();
        indicator.AnimationDuration.Should().Be(TimeSpan.FromMilliseconds(300));
        indicator.EnableAnimations.Should().BeTrue();
        indicator.FontSize.Should().Be(16);
    }

    [AvaloniaFact]
    public void StatusText_Property_Should_Update_Value()
    {
        // Arrange
        var indicator = new AnimatedStatusIndicator();

        // Act
        indicator.StatusText = "❌";

        // Assert
        indicator.StatusText.Should().Be("❌");
    }

    [AvaloniaFact]
    public void StatusColor_Property_Should_Update_Value()
    {
        // Arrange
        var indicator = new AnimatedStatusIndicator();
        var newBrush = Brushes.Red;

        // Act
        indicator.StatusColor = newBrush;

        // Assert
        indicator.StatusColor.Should().BeSameAs(newBrush);
    }

    [AvaloniaFact]
    public void AnimationDuration_Property_Should_Update_Value()
    {
        // Arrange
        var indicator = new AnimatedStatusIndicator();
        var newDuration = TimeSpan.FromSeconds(1);

        // Act
        indicator.AnimationDuration = newDuration;

        // Assert
        indicator.AnimationDuration.Should().Be(newDuration);
    }

    [AvaloniaFact]
    public void EnableAnimations_Property_Should_Update_Value()
    {
        // Arrange
        var indicator = new AnimatedStatusIndicator();

        // Act
        indicator.EnableAnimations = false;

        // Assert
        indicator.EnableAnimations.Should().BeFalse();
    }

    [AvaloniaFact]
    public void Should_Create_TextBlock_Content_On_Template_Apply()
    {
        // Arrange
        var indicator = new AnimatedStatusIndicator
        {
            StatusText = "⚠️",
            StatusColor = Brushes.Orange
        };

        // Act
        indicator.ApplyTemplate();

        // Assert
        indicator.Content.Should().BeOfType<TextBlock>();
        var textBlock = (TextBlock?)indicator.Content;
        textBlock?.Text.Should().Be("⚠️");
        textBlock?.Foreground.Should().Be(Brushes.Orange);
    }

    [AvaloniaFact]
    public void Should_Update_TextBlock_When_StatusText_Changes_Without_Animation()
    {
        // Arrange
        var indicator = new AnimatedStatusIndicator
        {
            EnableAnimations = false
        };
        indicator.ApplyTemplate();

        // Act
        indicator.StatusText = "New Status";

        // Assert
        var textBlock = (TextBlock?)indicator.Content;
        textBlock?.Text.Should().Be("New Status");
    }

    [AvaloniaFact]
    public void Should_Update_TextBlock_When_StatusColor_Changes_Without_Animation()
    {
        // Arrange
        var indicator = new AnimatedStatusIndicator
        {
            EnableAnimations = false
        };
        indicator.ApplyTemplate();

        // Act
        indicator.StatusColor = Brushes.Blue;

        // Assert
        var textBlock = (TextBlock?)indicator.Content;
        textBlock?.Foreground.Should().Be(Brushes.Blue);
    }

    [AvaloniaFact]
    public void Should_Handle_Null_TextBlock_Gracefully()
    {
        // Arrange
        var indicator = new AnimatedStatusIndicator();
        // Don't apply template, so _textBlock remains null

        // Act & Assert - Should not throw
        indicator.StatusText = "New Text";
        indicator.StatusColor = Brushes.Red;
    }

    [AvaloniaFact]
    public void Should_Apply_Transitions_When_Animations_Enabled()
    {
        // Arrange
        var indicator = new AnimatedStatusIndicator
        {
            EnableAnimations = true
        };

        // Act
        indicator.ApplyTemplate();

        // Assert
        var textBlock = (TextBlock?)indicator.Content;
        textBlock?.Transitions.Should().NotBeNull();
        textBlock?.Transitions.Should().HaveCount(1);
    }

    [AvaloniaFact]
    public void Should_Not_Apply_Transitions_When_Animations_Disabled()
    {
        // Arrange
        var indicator = new AnimatedStatusIndicator
        {
            EnableAnimations = false
        };

        // Act
        indicator.ApplyTemplate();

        // Assert
        var textBlock = (TextBlock?)indicator.Content;
        textBlock?.Transitions.Should().BeNull();
    }
}
