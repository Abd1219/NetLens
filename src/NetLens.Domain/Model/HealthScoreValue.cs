namespace NetLens.Domain.Model;

/// <summary>
/// Represents a quality or health score value ranging from 0 to 100.
/// </summary>
public readonly record struct HealthScoreValue
{
    public int Value { get; }

    public HealthScoreCategory Category => Value switch
    {
        >= 90 => HealthScoreCategory.Excellent,
        >= 70 => HealthScoreCategory.Good,
        >= 50 => HealthScoreCategory.Fair,
        >= 30 => HealthScoreCategory.Poor,
        _ => HealthScoreCategory.Critical
    };

    public HealthScoreValue(int value)
    {
        if (value < 0 || value > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Health score must be between 0 and 100.");
        }
        Value = value;
    }

    public override string ToString() => $"{Value} ({Category})";

    public static implicit operator int(HealthScoreValue score) => score.Value;
    public static explicit operator HealthScoreValue(int value) => new(value);
}

public enum HealthScoreCategory
{
    Critical,
    Poor,
    Fair,
    Good,
    Excellent
}
