using System.Globalization;
using System.Text.RegularExpressions;

namespace DomainCheck;

/// <summary>Normalisation and validation of domain names.</summary>
public static class DomainName
{
    private static readonly Regex Label = new("^(?!-)[a-z0-9-]{1,63}(?<!-)$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Normalises user input: removes scheme, path, port and trailing dot, lower-cases and converts
    /// internationalised names to punycode (<c>açaí.com.br</c> → <c>xn--aa-yja6c.com.br</c>).
    /// Returns null when the result is not a syntactically valid domain with at least two labels.
    /// </summary>
    /// <param name="input">E.g. <c>https://www.Loja.com.br/produtos</c>, <c>loja.com.br.</c>.</param>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var value = input.Trim();
        var scheme = value.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            value = value[(scheme + 3)..];
        }

        var cut = value.IndexOfAny(['/', '?', '#', ':']);
        if (cut >= 0)
        {
            value = value[..cut];
        }

        value = value.TrimEnd('.').ToLowerInvariant();
        // "www.loja.com.br" -> "loja.com.br", but "www.com.br" is itself a domain.
        var labelsNeeded = value.EndsWith(".br", StringComparison.Ordinal) ? 4 : 3;
        if (value.StartsWith("www.", StringComparison.Ordinal) && value.Split('.').Length >= labelsNeeded)
        {
            value = value[4..];
        }

        if (value.Any(c => c > 127))
        {
            try
            {
                value = new IdnMapping().GetAscii(value);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        var labels = value.Split('.');
        if (labels.Length < 2 || value.Length > 253 || !labels.All(l => Label.IsMatch(l)))
        {
            return null;
        }

        return value;
    }

    /// <summary>The public suffix the domain is registered under, for the extensions this library knows: <c>com.br</c>, <c>com</c>.</summary>
    /// <param name="domain">A normalised domain.</param>
    public static string Extension(string domain)
    {
        var labels = domain.Split('.');
        if (labels.Length >= 3 && labels[^1] == "br")
        {
            return labels[^2] + ".br";
        }

        return labels[^1];
    }

    /// <summary>True for any name under <c>.br</c>.</summary>
    /// <param name="domain">A normalised domain.</param>
    public static bool IsBr(string domain) => domain.EndsWith(".br", StringComparison.Ordinal);
}
