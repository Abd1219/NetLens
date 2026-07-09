namespace NetLens.Domain.Model;

/// <summary>
/// Represents the statistical variance in packet arrival times (Jitter) in milliseconds.
/// High jitter causes audio/video artifacts and indicates an unstable connection.
/// </summary>
public readonly record struct Jitter
{
    public double Milliseconds { get; }

    public Jitter(double milliseconds)
    {
        if (milliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(milliseconds), "Jitter cannot be negative.");

        Milliseconds = milliseconds;
    }

    public JitterCategory Category => Milliseconds switch
    {
        < 5 => JitterCategory.Excellent,
        < 15 => JitterCategory.Good,
        < 30 => JitterCategory.Fair,
        < 50 => JitterCategory.Poor,
        _ => JitterCategory.Critical
    };

    public override string ToString() => $"{Milliseconds:N1} ms";
}

public enum JitterCategory
{
    Excellent,
    Good,
    Fair,
    Poor,
    Critical
}
