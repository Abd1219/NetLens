namespace NetLens.Domain.Model;

/// <summary>
/// Represents the percentage of transmitted packets that failed to reach their destination.
/// Measured as a value from 0.0 (no loss) to 100.0 (total loss).
/// </summary>
public readonly record struct PacketLossRate
{
    public double Percentage { get; }

    public PacketLossRate(double percentage)
    {
        if (percentage < 0.0 || percentage > 100.0)
            throw new ArgumentOutOfRangeException(nameof(percentage), "Packet loss must be between 0 and 100 percent.");

        Percentage = percentage;
    }

    public PacketLossCategory Category => Percentage switch
    {
        0.0 => PacketLossCategory.None,
        < 1.0 => PacketLossCategory.Negligible,
        < 3.0 => PacketLossCategory.Low,
        < 10.0 => PacketLossCategory.Moderate,
        < 25.0 => PacketLossCategory.High,
        _ => PacketLossCategory.Severe
    };

    public bool IsAcceptable => Percentage < 3.0;

    public override string ToString() => $"{Percentage:N1}%";
}

public enum PacketLossCategory
{
    None,
    Negligible,
    Low,
    Moderate,
    High,
    Severe
}
