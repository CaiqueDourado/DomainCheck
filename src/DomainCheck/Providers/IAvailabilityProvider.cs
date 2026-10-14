namespace DomainCheck.Providers;

/// <summary>A source that can tell whether a domain is available.</summary>
public interface IAvailabilityProvider
{
    /// <summary>Short name used in <see cref="DomainCheckResult.Source"/>.</summary>
    string Name { get; }

    /// <summary>True when this provider handles <paramref name="domain"/>.</summary>
    /// <param name="domain">A normalised domain.</param>
    bool CanCheck(string domain);

    /// <summary>Checks <paramref name="domain"/>. Implementations should not throw for network errors; return <see cref="DomainStatus.Unknown"/> instead.</summary>
    /// <param name="domain">A normalised domain.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DomainCheckResult> CheckAsync(string domain, CancellationToken cancellationToken = default);
}
