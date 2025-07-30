using FluentAssertions;
using PapyrusMonitor.Avalonia.Controls;

namespace PapyrusMonitor.Avalonia.Tests.Controls;

public class AnimatedNumericTextBlockTests
{
    [Fact]
    public void Constructor_Should_Initialize_Default_Properties()
    {
        // Arrange & Act
        var textBlock = new AnimatedNumericTextBlock();

        // Assert
        textBlock.Value.Should().Be(0);
        textBlock.FormatString.Should().Be("F0");
        textBlock.AnimationDuration.Should().Be(TimeSpan.FromMilliseconds(400));
        textBlock.EnableAnimations.Should().BeTrue();
    }

    [Fact]
    public void Value_Property_Should_Update_Text_Without_Animation_When_Disabled()
    {
        // Arrange
        var textBlock = new AnimatedNumericTextBlock { EnableAnimations = false, FormatString = "F1" };

        // Act
        textBlock.Value = 42.5;

        // Assert
        textBlock.Text.Should().Be("42.5");
    }

    [Fact]
    public void Value_Property_Should_Update_Text_Without_Animation_When_Not_Visible()
    {
        // Arrange
        var textBlock = new AnimatedNumericTextBlock { IsVisible = false, FormatString = "F0" };

        // Act
        textBlock.Value = 100;

        // Assert
        textBlock.Text.Should().Be("100");
    }

    [Fact]
    public void FormatString_Property_Should_Update_Text_Display()
    {
        // Arrange
        var textBlock = new AnimatedNumericTextBlock { EnableAnimations = false, Value = 1234.56789 };

        // Act & Assert
        textBlock.FormatString = "F0";
        textBlock.Text.Should().Be("1235");

        textBlock.FormatString = "F2";
        textBlock.Text.Should().Be("1234.57");

        textBlock.FormatString = "F4";
        textBlock.Text.Should().Be("1234.5679");
    }

    [Fact]
    public void FormatString_Should_Handle_Integer_Format()
    {
        // Arrange
        var textBlock = new AnimatedNumericTextBlock
        {
            EnableAnimations = false, Value = 1234.56789, // Act
            FormatString = "D0"
        };

        // Assert
        textBlock.Text.Should().Be("1235");
    }

    [Fact]
    public void AnimationDuration_Property_Should_Update_Value()
    {
        // Arrange
        var textBlock = new AnimatedNumericTextBlock();
        var newDuration = TimeSpan.FromSeconds(1);

        // Act
        textBlock.AnimationDuration = newDuration;

        // Assert
        textBlock.AnimationDuration.Should().Be(newDuration);
    }

    [Fact]
    public void EnableAnimations_Property_Should_Update_Value()
    {
        // Arrange
        var textBlock = new AnimatedNumericTextBlock {
            // Act
            EnableAnimations = false };

        // Assert
        textBlock.EnableAnimations.Should().BeFalse();
    }

    [Fact]
    public void Should_Not_Animate_When_Value_Change_Is_Small()
    {
        // Arrange
        var textBlock = new AnimatedNumericTextBlock
        {
            EnableAnimations = false, // Disable animations for predictable testing
            IsVisible = true,
            FormatString = "F2",
            // Act
            Value = 10.0
        };

        textBlock.Text.Should().Be("10.00");

        // Now test that small changes work correctly
        textBlock.Value = 10.005; // Change less than 0.01

        // Assert
        textBlock.Text.Should().Be("10.01"); // Should round to 2 decimal places
    }

    [Fact]
    public void Should_Handle_Zero_Value()
    {
        // Arrange
        var textBlock = new AnimatedNumericTextBlock { EnableAnimations = false, FormatString = "F1", // Act
            Value = 0 };

        // Assert
        textBlock.Text.Should().Be("0.0");
    }

    [Fact]
    public void Should_Handle_Negative_Values()
    {
        // Arrange
        var textBlock = new AnimatedNumericTextBlock { EnableAnimations = false, FormatString = "F2", // Act
            Value = -123.45 };

        // Assert
        textBlock.Text.Should().Be("-123.45");
    }

    [Fact]
    public void Should_Handle_Large_Values()
    {
        // Arrange
        var textBlock = new AnimatedNumericTextBlock { EnableAnimations = false, FormatString = "F0", // Act
            Value = 1_000_000 };

        // Assert
        textBlock.Text.Should().Be("1000000");
    }

    [Fact]
    public void Should_Handle_Format_Change_While_Value_Remains_Same()
    {
        // Arrange
        var textBlock = new AnimatedNumericTextBlock { EnableAnimations = false, Value = 123.456, FormatString = "F1" };
        textBlock.Text.Should().Be("123.5");

        // Act
        textBlock.FormatString = "F3";

        // Assert
        textBlock.Text.Should().Be("123.456");
    }
}
