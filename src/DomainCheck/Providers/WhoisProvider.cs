using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using DomainCheck.Internal;

namespace DomainCheck.Providers;

/// <summary>Sends a WHOIS query (RFC 3912) and returns the answer.</summary>
public interface IWhoisClient
{
    /// <summary>Queries <paramref name="server"/> on port 43.</summary>
    /// <param name="server">WHOIS server host name.</param>
    /// <param name="query">The query line (usually the domain).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string> QueryAsync(string server, string query, CancellationToken cancellationToken = default);
}

/// <summary>WHOIS over TCP port 43, with a timeout and a size limit.</summary>
public sealed class TcpWhoisClient : IWhoisClient
{
    private readonly TimeSpan _timeout;

    /// <summary>Creates the client.</summary>
    /// <param name="timeout">Timeout per query; null = 15 seconds.</param>
    public TcpWhoisClient(TimeSpan? timeout = null) => _timeout = timeout ?? TimeSpan.FromSeconds(15);

    /// <inheritdoc />
    public async Task<string> QueryAsync(string server, string query, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(server, 43, timeout.Token).ConfigureAwait(false);
        await using var stream = tcp.GetStream();

        var bytes = Encoding.ASCII.GetBytes(query + "\r\n");
        await stream.WriteAsync(bytes, timeout.Token).ConfigureAwait(false);

        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, timeout.Token).ConfigureAwait(false)) > 0 && buffer.Length < 1_000_000)
        {
            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}

/// <summary>Options for <see cref="WhoisProvider"/>.</summary>
public sealed class WhoisOptions
{
    /// <summary>Minimum interval between queries to the same server. Default: 1 second.</summary>
    public TimeSpan MinimumIntervalPerServer { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Server used to discover the WHOIS server of each extension. Default: <c>whois.iana.org</c>.</summary>
    public string IanaServer { get; set; } = "whois.iana.org";

    /// <summary>Known servers by extension, so IANA is not asked every time. You can add or override entries.</summary>
    public IDictionary<string, string> Servers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["com"] = "whois.verisign-grs.com",
        ["net"] = "whois.verisign-grs.com",
        ["org"] = "whois.publicinterestregistry.org",
        ["br"] = "whois.registro.br",
    };
}

/// <summary>
/// Checks any extension with WHOIS. The WHOIS server of each extension is discovered from IANA (and cached);
/// answers are interpreted with patterns used by the common registries.
/// </summary>
public sealed class WhoisProvider : IAvailabilityProvider
{
    private static readonly RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    private static readonly Regex NotFound = new(
        "^\\s*%?\\s*(no match for|not found|no data found|no entries found|domain not found|no object found|object does not exist|% no such domain|status:\\s*(free|available)|the queried object does not exist)" +
        "|is available for registration|no matching record",
        Options, Timeout);

    private static readonly Regex RateLimited = new("limit exceeded|quota exceeded|try again later|too many (queries|requests)", Options, Timeout);

    private static readonly Regex Registered = new("^\\s*(domain name|domain|nome de domínio)\\s*:", Options, Timeout);

    private static readonly Regex Expiry = new(
        "^\\s*(registry expiry date|registrar registration expiration date|expiration date|expiry date|expires on|expires|paid-till|expire)\\s*:\\s*(\\S+)",
        Options, Timeout);

    private static readonly Regex Refer = new("^\\s*(refer|whois)\\s*:\\s*(\\S+)", Options, Timeout);

    private readonly IWhoisClient _client;
    private readonly WhoisOptions _options;
    private readonly ConcurrentDictionary<string, RateGate> _gates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the provider.</summary>
    /// <param name="client">WHOIS client; null = <see cref="TcpWhoisClient"/>.</param>
    /// <param name="options">Options; null = defaults.</param>
    public WhoisProvider(IWhoisClient? client = null, WhoisOptions? options = null)
    {
        _client = client ?? new TcpWhoisClient();
        _options = options ?? new WhoisOptions();
    }

    /// <inheritdoc />
    public string Name => "whois";

    /// <inheritdoc />
    public bool CanCheck(string domain) => true;

    /// <inheritdoc />
    public async Task<DomainCheckResult> CheckAsync(string domain, CancellationToken cancellationToken = default)
    {
        var tld = domain[(domain.LastIndexOf('.') + 1)..];
        string? server;
        try
        {
            server = await ServerForAsync(tld, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsNetwork(ex) && !cancellationToken.IsCancellationRequested)
        {
            return new DomainCheckResult(domain, DomainStatus.Unknown, Name, Detail: "IANA lookup failed: " + ex.Message);
        }

        if (server is null)
        {
            return new DomainCheckResult(domain, DomainStatus.Unknown, Name, Detail: $"No WHOIS server known for .{tld}.");
        }

        try
        {
            var answer = await QueryAsync(server, domain, cancellationToken).ConfigureAwait(false);
            return Parse(domain, answer, "whois:" + server);
        }
        catch (Exception ex) when (IsNetwork(ex) && !cancellationToken.IsCancellationRequested)
        {
            return new DomainCheckResult(domain, DomainStatus.Unknown, "whois:" + server, Detail: ex.Message);
        }
    }

    /// <summary>Interprets a WHOIS answer.</summary>
    /// <param name="domain">The domain that was asked.</param>
    /// <param name="answer">The WHOIS text.</param>
    /// <param name="source">Value for <see cref="DomainCheckResult.Source"/>.</param>
    public static DomainCheckResult Parse(string domain, string answer, string source = "whois")
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return new DomainCheckResult(domain, DomainStatus.Unknown, source, Detail: "Empty answer.", Raw: answer);
        }

        if (RateLimited.IsMatch(answer))
        {
            return new DomainCheckResult(domain, DomainStatus.Unknown, source, Detail: "Rate limited by the WHOIS server.", Raw: answer);
        }

        if (NotFound.IsMatch(answer))
        {
            return new DomainCheckResult(domain, DomainStatus.Available, source, Raw: answer);
        }

        if (Registered.IsMatch(answer))
        {
            DateTimeOffset? expires = null;
            var match = Expiry.Match(answer);
            if (match.Success)
            {
                var text = match.Groups[2].Value;
                if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                    || DateTimeOffset.TryParseExact(text, ["yyyyMMdd", "yyyy.MM.dd", "dd-MMM-yyyy", "dd/MM/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsed))
                {
                    expires = parsed;
                }
            }

            return new DomainCheckResult(domain, DomainStatus.Registered, source, expires, Raw: answer);
        }

        return new DomainCheckResult(domain, DomainStatus.Unknown, source, Detail: "Unrecognised WHOIS answer.", Raw: answer);
    }

    private async Task<string?> ServerForAsync(string tld, CancellationToken cancellationToken)
    {
        if (_options.Servers.TryGetValue(tld, out var known))
        {
            return known;
        }

        var answer = await QueryAsync(_options.IanaServer, tld, cancellationToken).ConfigureAwait(false);
        var match = Refer.Match(answer);
        if (!match.Success)
        {
            return null;
        }

        var server = match.Groups[2].Value.Trim();
        lock (_options.Servers)
        {
            _options.Servers[tld] = server;
        }

        return server;
    }

    private async Task<string> QueryAsync(string server, string query, CancellationToken cancellationToken)
    {
        var gate = _gates.GetOrAdd(server, _ => new RateGate(_options.MinimumIntervalPerServer));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return await _client.QueryAsync(server, query, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsNetwork(Exception ex) => ex is SocketException or IOException or OperationCanceledException;
}
