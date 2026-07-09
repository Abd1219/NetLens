namespace NetLens.Domain.Model;

/// <summary>
/// Represents a round-trip network latency measurement in milliseconds.
/// </summary>
public readonly record struct Latency
{
    public static readonly Latency Timeout = new(-1);

    public double Milliseconds { get; }

    /// <summary>
    /// Returns true if the probe timed out and no response was received.
    /// </summary>
    public bool IsTimeout => Milliseconds < 0;

    public Latency(double milliseconds)
    {
        Milliseconds = milliseconds;
    }

    public LatencyCategory Category => Milliseconds switch
    {
        < 0 => LatencyCategory.Timeout,
        < 20 => LatencyCategory.Excellent,
        < 50 => LatencyCategory.Good,
        < 100 => LatencyCategory.Fair,
        < 200 => LatencyCategory.Poor,
        _ => LatencyCategory.Critical
    };

    public override string ToString() => IsTimeout ? "Timeout" : $"{Milliseconds:N1} ms";
}

public enum LatencyCategory
{
    Excellent,
    Good,
    Fair,
    Poor,
    Critical,
    Timeout
}
