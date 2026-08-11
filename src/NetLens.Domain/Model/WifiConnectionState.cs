namespace NetLens.Domain.Model;

/// <summary>
/// Represents the connection state of the wireless network interface.
/// Strongly typed domain enum matching Windows interface state semantics.
/// </summary>
public enum WifiConnectionState
{
    Unknown = 0,
    Connected = 1,
    Disconnected = 2,
    Associating = 3,
    Authenticating = 4,
    Disconnecting = 5,
    NotReady = 6
}

public static class WifiConnectionStateExtensions
{
    public static string ToDisplayString(this WifiConnectionState state) => state switch
    {
        WifiConnectionState.Connected => "Connected",
        WifiConnectionState.Disconnected => "Disconnected",
        WifiConnectionState.Associating => "Associating",
        WifiConnectionState.Authenticating => "Authenticating",
        WifiConnectionState.Disconnecting => "Disconnecting",
        WifiConnectionState.NotReady => "Not Ready",
        _ => "Unavailable"
    };
}
