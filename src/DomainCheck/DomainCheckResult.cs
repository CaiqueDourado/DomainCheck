namespace DomainCheck;

/// <summary>Availability of a domain name.</summary>
public enum DomainStatus
{
    /// <summary>Not registered: it can be registered now.</summary>
    Available,

    /// <summary>Already registered.</summary>
    Registered,

    /// <summary>Not registered but cannot be registered now (reserved, blocked, in a release process).</summary>
    Unavailable,

    /// <summary>The name is not a valid domain for that registry.</summary>
    Invalid,

    /// <summary>Could not be determined (error, rate limit, unrecognised answer).</summary>
    Unknown,
}

/// <summary>The answer for one domain.</summary>
/// <param name="Domain">The domain checked, normalised (lower case, ASCII/punycode).</param>
/// <param name="Status">Availability.</param>
/// <param name="Source">Who answered: <c>dns</c>, <c>registro.br</c> or <c>whois:&lt;server&gt;</c>.</param>
/// <param name="ExpiresAt">Expiration date, when the source provides it.</param>
/// <param name="Detail">Human-readable detail (registry status, error message).</param>
/// <param name="Raw">The raw answer of the source, for auditing.</param>
public sealed record DomainCheckResult(
    string Domain,
    DomainStatus Status,
    string Source,
    DateTimeOffset? ExpiresAt = null,
    string? Detail = null,
    string? Raw = null)
{
    /// <summary>True when <see cref="Status"/> is <see cref="DomainStatus.Available"/>.</summary>
    public bool IsAvailable => Status == DomainStatus.Available;
}
