namespace NetLens.Domain.Model;

/// <summary>
/// A derived value object representing the wireless signal quality as a percentage (0-100).
/// Calculated from the RSSI using a non-linear scale matching Windows' own quality formula.
/// </summary>
public readonly record struct SignalQuality
{
    public int Percentage { get; }

    public SignalQuality(int percentage)
    {
        if (percentage < 0 || percentage > 100)
            throw new ArgumentOutOfRangeException(nameof(percentage), "Signal quality must be between 0 and 100.");

        Percentage = percentage;
    }

    /// <summary>
    /// Derives signal quality from an RSSI value using the Windows WLAN quality scale.
    /// RSSI of -50 dBm = 100%, RSSI of -100 dBm = 0%.
    /// </summary>
    public static SignalQuality FromRssi(RSSI rssi)
    {
        var quality = (rssi.Value + 100) * 2;
        quality = Math.Clamp(quality, 0, 100);
        return new SignalQuality(quality);
    }

    public SignalQualityLevel Level => Percentage switch
    {
        >= 80 => SignalQualityLevel.Excellent,
        >= 60 => SignalQualityLevel.Good,
        >= 40 => SignalQualityLevel.Fair,
        >= 20 => SignalQualityLevel.Poor,
        _ => SignalQualityLevel.NoSignal
    };

    public override string ToString() => $"{Percentage}%";
}

public enum SignalQualityLevel
{
    NoSignal,
    Poor,
    Fair,
    Good,
    Excellent
}
