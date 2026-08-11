namespace NetLens.Domain.Model;

/// <summary>
/// Represents wireless security and authentication standards.
/// Strongly typed domain enum.
/// </summary>
public enum WifiSecurityType
{
    Unknown = 0,
    Open = 1,
    Wep = 2,
    WpaPersonal = 3,
    WpaEnterprise = 4,
    Wpa2Personal = 5,
    Wpa2Enterprise = 6,
    Wpa3Personal = 7,
    Wpa3Enterprise = 8
}

public static class WifiSecurityTypeExtensions
{
    public static string ToDisplayString(this WifiSecurityType security) => security switch
    {
        WifiSecurityType.Open => "Open (None)",
        WifiSecurityType.Wep => "WEP",
        WifiSecurityType.WpaPersonal => "WPA-Personal",
        WifiSecurityType.WpaEnterprise => "WPA-Enterprise",
        WifiSecurityType.Wpa2Personal => "WPA2-Personal",
        WifiSecurityType.Wpa2Enterprise => "WPA2-Enterprise",
        WifiSecurityType.Wpa3Personal => "WPA3-Personal",
        WifiSecurityType.Wpa3Enterprise => "WPA3-Enterprise",
        _ => "Unavailable"
    };

    public static WifiSecurityType FromNativeAuthAlgo(uint authAlgo) => authAlgo switch
    {
        1 => WifiSecurityType.Open,
        2 => WifiSecurityType.Wep,
        3 => WifiSecurityType.WpaEnterprise,
        4 => WifiSecurityType.WpaPersonal,
        5 => WifiSecurityType.Wpa2Enterprise,
        6 => WifiSecurityType.Wpa2Personal,
        7 => WifiSecurityType.Wpa3Personal,
        8 => WifiSecurityType.Wpa3Enterprise,
        _ => WifiSecurityType.Unknown
    };
}
