namespace NetLens.Domain.Model;

/// <summary>
/// Represents the physical layer link speed (PHY Rate) in Megabits per second (Mbps).
/// </summary>
public readonly record struct PhyRate
{
    public double Value { get; }

    public PhyRate(double value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "PHY Rate cannot be negative.");
        }
        Value = value;
    }

    public override string ToString() => $"{Value:N1} Mbps";

    public static implicit operator double(PhyRate rate) => rate.Value;
    public static explicit operator PhyRate(double value) => new(value);
}
