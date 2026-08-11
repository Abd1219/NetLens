namespace NetLens.Domain.Model;

/// <summary>
/// Represents standard Wi-Fi frequency bands.
/// Strongly typed domain enum.
/// </summary>
public enum WifiBand
{
    Unknown = 0,
    Band2_4GHz = 1,
    Band5GHz = 2,
    Band6GHz = 3
}

public static class WifiBandExtensions
{
    public static string ToDisplayString(this WifiBand band) => band switch
    {
        WifiBand.Band2_4GHz => "2.4 GHz",
        WifiBand.Band5GHz => "5 GHz",
        WifiBand.Band6GHz => "6 GHz",
        _ => "Unavailable"
    };

    public static WifiBand FromFrequencyMhz(int freqMhz)
    {
        if (freqMhz >= 2412 && freqMhz <= 2484) return WifiBand.Band2_4GHz;
        if (freqMhz >= 5180 && freqMhz <= 5885) return WifiBand.Band5GHz;
        if (freqMhz >= 5955 && freqMhz <= 7115) return WifiBand.Band6GHz;
        return WifiBand.Unknown;
    }
}
