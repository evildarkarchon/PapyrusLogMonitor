using PapyrusMonitor.Core.Models;

namespace PapyrusMonitor.Core.Tests.Models;

public class PapyrusStatsTests
{
    [Fact]
    public void PapyrusStats_Equality_ComparesCoreValues()
    {
        // Arrange
        var timestamp1 = DateTime.Now;
        var timestamp2 = timestamp1.AddMinutes(1);

        var stats1 = new PapyrusStats(timestamp1, 5, 10, 2, 1, 0.5);
        var stats2 = new PapyrusStats(timestamp2, 5, 10, 2, 1, 0.6); // Different timestamp and ratio

        // Act & Assert
        Assert.True(stats1.Equals(stats2)); // Should be equal despite different timestamp/ratio
    }

    [Fact]
    public void PapyrusStats_Equality_DifferentCoreValues()
    {
        // Arrange
        var timestamp = DateTime.Now;
        var stats1 = new PapyrusStats(timestamp, 5, 10, 2, 1, 0.5);
        var stats2 = new PapyrusStats(timestamp, 6, 10, 2, 1, 0.5); // Different dumps

        // Act & Assert
        Assert.False(stats1.Equals(stats2));
    }

    [Fact]
    public void PapyrusStats_GetHashCode_ConsistentForEqualObjects()
    {
        // Arrange
        var timestamp1 = DateTime.Now;
        var timestamp2 = timestamp1.AddHours(1);

        var stats1 = new PapyrusStats(timestamp1, 5, 10, 2, 1, 0.5);
        var stats2 = new PapyrusStats(timestamp2, 5, 10, 2, 1, 0.8); // Different timestamp and ratio

        // Act
        var hash1 = stats1.GetHashCode();
        var hash2 = stats2.GetHashCode();

        // Assert
        Assert.Equal(hash1, hash2); // Should have same hash since core values are equal
    }

    [Fact]
    public void PapyrusStats_Constructor_SetsAllProperties()
    {
        // Arrange
        var timestamp = DateTime.Now;
        const int dumps = 5;
        const int stacks = 10;
        const int warnings = 2;
        const int errors = 1;
        const double ratio = 0.5;

        // Act
        var stats = new PapyrusStats(timestamp, dumps, stacks, warnings, errors, ratio);

        // Assert
        Assert.Equal(timestamp, stats.Timestamp);
        Assert.Equal(dumps, stats.Dumps);
        Assert.Equal(stacks, stats.Stacks);
        Assert.Equal(warnings, stats.Warnings);
        Assert.Equal(errors, stats.Errors);
        Assert.Equal(ratio, stats.Ratio);
    }
}
