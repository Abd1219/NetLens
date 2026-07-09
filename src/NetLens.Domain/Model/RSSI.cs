namespace NetLens.Domain.Model;

/// <summary>
/// Represents Received Signal Strength Indication (RSSI) bounded between -100 and 0 dBm.
/// </summary>
public readonly record struct RSSI
{
    public int Value { get; }

    public RSSI(int value)
    {
        if (value < -100 || value > 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "RSSI value must be between -100 and 0 dBm.");
        }
        Value = value;
    }

    public override string ToString() => $"{Value} dBm";

    public static implicit operator int(RSSI rssi) => rssi.Value;
    public static explicit operator RSSI(int value) => new(value);
}
