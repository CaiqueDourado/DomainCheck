using System.Net;
using System.Net.Sockets;

namespace DomainCheck;

/// <summary>Resolves host names. Replace it in tests or to use a specific DNS server.</summary>
public interface IDnsResolver
{
    /// <summary>
    /// True when the name resolves to at least one address, false when the DNS says it does not exist,
    /// null when the lookup failed for another reason.
    /// </summary>
    /// <param name="host">Host name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool?> ResolvesAsync(string host, CancellationToken cancellationToken = default);
}

/// <summary>Uses the operating system resolver.</summary>
public sealed class SystemDnsResolver : IDnsResolver
{
    /// <inheritdoc />
    public async Task<bool?> ResolvesAsync(string host, CancellationToken cancellationToken = default)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            return addresses.Length > 0;
        }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData)
        {
            return false;
        }
        catch (SocketException)
        {
            return null;
        }
    }
}
