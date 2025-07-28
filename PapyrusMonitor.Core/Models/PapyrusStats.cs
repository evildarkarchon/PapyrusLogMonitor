namespace PapyrusMonitor.Core.Models;

/// <summary>
/// Represents statistics associated with Papyrus, including performance and error metrics.
/// 
/// This record encapsulates various statistics related to Papyrus, such as the number of dumps, 
/// stacks, warnings, errors, and a calculated ratio. It provides value-based equality comparison 
/// based on all attributes.
/// </summary>
/// <param name="Timestamp">The timestamp indicating when the statistics were recorded</param>
/// <param name="Dumps">The number of dumps processed or analyzed</param>
/// <param name="Stacks">The number of stacks involved or recorded</param>
/// <param name="Warnings">The number of warnings encountered</param>
/// <param name="Errors">The number of errors encountered</param>
/// <param name="Ratio">A calculated ratio or metric based on dumps/stacks</param>
public record PapyrusStats(
    DateTime Timestamp,
    int Dumps,
    int Stacks,
    int Warnings,
    int Errors,
    double Ratio)
{
    /// <summary>
    /// Determines equality based on the core statistics values (excluding timestamp).
    /// This allows comparing stats content while ignoring when they were recorded.
    /// </summary>
    /// <param name="other">The other PapyrusStats to compare with</param>
    /// <returns>True if the statistics values are equal</returns>
    public virtual bool Equals(PapyrusStats? other)
    {
        return other is not null 
            && Dumps == other.Dumps 
            && Stacks == other.Stacks 
            && Warnings == other.Warnings 
            && Errors == other.Errors;
    }

    /// <summary>
    /// Returns a hash code based on the core statistics values (excluding timestamp).
    /// This ensures that instances with the same statistics have the same hash.
    /// </summary>
    /// <returns>Hash code for this instance</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(Dumps, Stacks, Warnings, Errors);
    }
}