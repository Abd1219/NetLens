using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace NetLens.Network.Discovery;

/// <summary>
/// Resolves MAC addresses using Windows SendARP P/Invoke.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ArpResolver
{
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int SendARP(uint destIp, uint srcIp, byte[] macAddr, ref uint physicalAddrLen);

    public string? ResolveMAC(string ipAddress)
    {
        try
        {
            if (!IPAddress.TryParse(ipAddress, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return null;
            }

            var destBytes = ip.GetAddressBytes();
            uint destIp = BitConverter.ToUInt32(destBytes, 0);

            var macBytes = new byte[6];
            uint macLen = (uint)macBytes.Length;

            int result = SendARP(destIp, 0, macBytes, ref macLen);
            if (result == 0)
            {
                return string.Join(":", macBytes.Select(b => b.ToString("X2")));
            }
        }
        catch
        {
            // Fail silently
        }

        return null;
    }
}
