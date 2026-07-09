namespace NetLens.Domain.Model;

/// <summary>
/// Represents a measured network bandwidth throughput in Megabits per second (Mbps).
/// </summary>
public readonly record struct Bandwidth
{
    public double Mbps { get; }

    public Bandwidth(double mbps)
    {
        if (mbps < 0)
            throw new ArgumentOutOfRangeException(nameof(mbps), "Bandwidth cannot be negative.");

        Mbps = mbps;
    }

    public override string ToString() => Mbps >= 1000
        ? $"{Mbps / 1000:N2} Gbps"
        : $"{Mbps:N1} Mbps";

    public static implicit operator double(Bandwidth b) => b.Mbps;
    public static explicit operator Bandwidth(double mbps) => new(mbps);
}
