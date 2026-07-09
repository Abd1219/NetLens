namespace NetLens.Domain.Model;

/// <summary>
/// Represents a wireless frequency band and raw frequency in MHz.
/// </summary>
public readonly record struct Frequency
{
    public int ValueMhz { get; }

    public FrequencyBand Band => ValueMhz switch
    {
        >= 2400 and <= 2500 => FrequencyBand.Band2_4GHz,
        >= 4900 and <= 5900 => FrequencyBand.Band5GHz,
        >= 5925 and <= 7125 => FrequencyBand.Band6GHz,
        _ => FrequencyBand.Unknown
    };

    public Frequency(int valueMhz)
    {
        if (valueMhz < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valueMhz), "Frequency cannot be negative.");
        }
        ValueMhz = valueMhz;
    }

    public override string ToString()
    {
        return Band switch
        {
            FrequencyBand.Band2_4GHz => $"{ValueMhz} MHz (2.4 GHz)",
            FrequencyBand.Band5GHz => $"{ValueMhz} MHz (5 GHz)",
            FrequencyBand.Band6GHz => $"{ValueMhz} MHz (6 GHz)",
            _ => $"{ValueMhz} MHz"
        };
    }
}

public enum FrequencyBand
{
    Unknown,
    Band2_4GHz,
    Band5GHz,
    Band6GHz
}
