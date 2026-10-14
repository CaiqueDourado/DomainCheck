using System.Runtime.CompilerServices;
using DomainCheck.Providers;

namespace DomainCheck;

/// <summary>Options for <see cref="DomainChecker"/>.</summary>
public sealed class DomainCheckerOptions
{
    /// <summary>
    /// Resolve the name first: a name that resolves is registered, with no need to ask the registry.
    /// Names that do not resolve still go to the registry (many registered domains have no DNS). Default: true.
    /// </summary>
    public bool UseDnsPrecheck { get; set; } = true;

    /// <summary>Maximum domains checked at the same time by <see cref="DomainChecker.CheckManyAsync"/>. Default: 4.</summary>
    public int MaxConcurrency { get; set; } = 4;
}

/// <summary>
/// Checks domain availability: DNS pre-check, then the first provider that handles the extension
/// (by default Registro.br for <c>.br</c> and WHOIS for the rest).
/// </summary>
/// <example>
/// <code>
/// using var checker = DomainChecker.CreateDefault();
/// var result = await checker.CheckAsync("minhaloja.com.br");
/// Console.WriteLine($"{result.Domain}: {result.Status} ({result.Source})");
/// </code>
/// </example>
public sealed class DomainChecker : IDisposable
{
    private readonly IReadOnlyList<IAvailabilityProvider> _providers;
    private readonly IDnsResolver? _dns;
    private readonly DomainCheckerOptions _options;
    private readonly IDisposable? _owned;

    /// <summary>Creates a checker with explicit providers.</summary>
    /// <param name="providers">Providers, in priority order.</param>
    /// <param name="dns">Resolver for the pre-check; null = the system resolver.</param>
    /// <param name="options">Options; null = defaults.</param>
    public DomainChecker(IEnumerable<IAvailabilityProvider> providers, IDnsResolver? dns = null, DomainCheckerOptions? options = null)
        : this(providers, dns, options, null)
    {
    }

    private DomainChecker(IEnumerable<IAvailabilityProvider> providers, IDnsResolver? dns, DomainCheckerOptions? options, IDisposable? owned)
    {
        _providers = providers.ToList();
        _options = options ?? new DomainCheckerOptions();
        _dns = _options.UseDnsPrecheck ? dns ?? new SystemDnsResolver() : null;
        _owned = owned;
    }

    /// <summary>Creates a checker with Registro.br for <c>.br</c> and WHOIS for everything else.</summary>
    /// <param name="options">Options; null = defaults.</param>
    /// <param name="registroBr">Registro.br options; null = defaults.</param>
    /// <param name="whois">WHOIS options; null = defaults.</param>
    public static DomainChecker CreateDefault(DomainCheckerOptions? options = null, RegistroBrOptions? registroBr = null, WhoisOptions? whois = null)
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        return new DomainChecker(
            [new RegistroBrProvider(http, registroBr), new WhoisProvider(null, whois)],
            null,
            options,
            http);
    }

    /// <summary>Checks one domain.</summary>
    /// <param name="domain">Domain as typed by a user (scheme, <c>www.</c>, path and upper case are accepted).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<DomainCheckResult> CheckAsync(string domain, CancellationToken cancellationToken = default)
    {
        var normalized = DomainName.Normalize(domain);
        if (normalized is null)
        {
            return new DomainCheckResult(domain?.Trim() ?? string.Empty, DomainStatus.Invalid, "syntax", Detail: "Not a valid domain name.");
        }

        if (_dns is not null && await _dns.ResolvesAsync(normalized, cancellationToken).ConfigureAwait(false) == true)
        {
            return new DomainCheckResult(normalized, DomainStatus.Registered, "dns", Detail: "The name resolves.");
        }

        var provider = _providers.FirstOrDefault(p => p.CanCheck(normalized));
        if (provider is null)
        {
            return new DomainCheckResult(normalized, DomainStatus.Unknown, "none", Detail: "No provider handles this extension.");
        }

        return await provider.CheckAsync(normalized, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Checks many domains, at most <see cref="DomainCheckerOptions.MaxConcurrency"/> at a time, yielding results as they finish.</summary>
    /// <param name="domains">Domains to check (duplicates are checked once).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async IAsyncEnumerable<DomainCheckResult> CheckManyAsync(IEnumerable<string> domains, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Not disposed: tasks still running when the caller stops enumerating release it later.
        var throttle = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrency));
        var pending = new List<Task<DomainCheckResult>>();

        foreach (var domain in domains.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            pending.Add(RunAsync(domain));

            // Yield whatever already finished, keeping memory flat for long lists.
            foreach (var done in pending.Where(t => t.IsCompleted).ToList())
            {
                pending.Remove(done);
                yield return await done.ConfigureAwait(false);
            }
        }

        while (pending.Count > 0)
        {
            var done = await Task.WhenAny(pending).ConfigureAwait(false);
            pending.Remove(done);
            yield return await done.ConfigureAwait(false);
        }

        async Task<DomainCheckResult> RunAsync(string domain)
        {
            try
            {
                return await CheckAsync(domain, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                throttle.Release();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _owned?.Dispose();
}
