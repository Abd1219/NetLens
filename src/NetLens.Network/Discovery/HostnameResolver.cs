using System.Net;

namespace NetLens.Network.Discovery;

/// <summary>
/// Performs reverse DNS hostname resolution with a built-in timeout.
/// </summary>
public sealed class HostnameResolver
{
    public async Task<string?> ResolveHostnameAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            var hostEntry = await Dns.GetHostEntryAsync(ipAddress)
                .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken)
                .ConfigureAwait(false);

            return hostEntry.HostName;
        }
        catch
        {
            return null;
        }
    }
}
