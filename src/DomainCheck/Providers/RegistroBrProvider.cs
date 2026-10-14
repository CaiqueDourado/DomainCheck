using System.Globalization;
using System.Text.Json;
using DomainCheck.Internal;

namespace DomainCheck.Providers;

/// <summary>Options for <see cref="RegistroBrProvider"/>.</summary>
public sealed class RegistroBrOptions
{
    /// <summary>
    /// Endpoint, with <c>{0}</c> where the domain goes. Default: the availability endpoint used by the
    /// Registro.br website (<c>https://registro.br/v2/ajax/avail/raw/{0}</c>). It is not a documented public API
    /// and may change; it is configurable for that reason.
    /// </summary>
    public string EndpointFormat { get; set; } = "https://registro.br/v2/ajax/avail/raw/{0}";

    /// <summary>Minimum interval between requests. Default: 1 second.</summary>
    public TimeSpan MinimumInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>User-Agent sent with requests.</summary>
    public string UserAgent { get; set; } = "DomainCheck/0.1 (+https://github.com/CaiqueDourado/DomainCheck)";

    /// <summary>
    /// How Registro.br <c>status</c> codes map to <see cref="DomainStatus"/>. Codes not listed become
    /// <see cref="DomainStatus.Unknown"/> with the code in <see cref="DomainCheckResult.Detail"/>.
    /// </summary>
    public IDictionary<int, DomainStatus> StatusMap { get; } = new Dictionary<int, DomainStatus>
    {
        [0] = DomainStatus.Available,
        [2] = DomainStatus.Registered,
        [3] = DomainStatus.Invalid,
    };
}

/// <summary>
/// Checks <c>.br</c> domains with Registro.br. Also validates locally the rules Registro.br enforces
/// (2 to 26 characters in the name) to avoid useless requests.
/// </summary>
public sealed class RegistroBrProvider : IAvailabilityProvider
{
    private readonly HttpClient _http;
    private readonly RegistroBrOptions _options;
    private readonly RateGate _gate;

    /// <summary>Creates the provider.</summary>
    /// <param name="httpClient">HTTP client to use.</param>
    /// <param name="options">Options; null = defaults.</param>
    public RegistroBrProvider(HttpClient httpClient, RegistroBrOptions? options = null)
    {
        _http = httpClient;
        _options = options ?? new RegistroBrOptions();
        _gate = new RateGate(_options.MinimumInterval);
    }

    /// <inheritdoc />
    public string Name => "registro.br";

    /// <inheritdoc />
    public bool CanCheck(string domain) => DomainName.IsBr(domain);

    /// <inheritdoc />
    public async Task<DomainCheckResult> CheckAsync(string domain, CancellationToken cancellationToken = default)
    {
        var name = domain.Split('.')[0];
        if (name.Length is < 2 or > 26)
        {
            return new DomainCheckResult(domain, DomainStatus.Invalid, Name, Detail: "Registro.br names have 2 to 26 characters.");
        }

        string body;
        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            var url = string.Format(CultureInfo.InvariantCulture, _options.EndpointFormat, Uri.EscapeDataString(domain));
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new DomainCheckResult(domain, DomainStatus.Unknown, Name, Detail: $"HTTP {(int)response.StatusCode}", Raw: body);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return new DomainCheckResult(domain, DomainStatus.Unknown, Name, Detail: ex.Message);
        }

        return Parse(domain, body, _options.StatusMap);
    }

    /// <summary>Interprets a Registro.br availability answer.</summary>
    /// <param name="domain">The domain that was asked.</param>
    /// <param name="json">The JSON answer.</param>
    /// <param name="statusMap">Status code mapping; null = the defaults of <see cref="RegistroBrOptions"/>.</param>
    public static DomainCheckResult Parse(string domain, string json, IDictionary<int, DomainStatus>? statusMap = null)
    {
        statusMap ??= new RegistroBrOptions().StatusMap;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("status", out var statusElement) || !statusElement.TryGetInt32(out var code))
            {
                return new DomainCheckResult(domain, DomainStatus.Unknown, "registro.br", Detail: "Answer without 'status'.", Raw: json);
            }

            var status = statusMap.TryGetValue(code, out var mapped) ? mapped : DomainStatus.Unknown;

            DateTimeOffset? expires = null;
            if (root.TryGetProperty("expires-at", out var exp) && exp.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(exp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                expires = parsed;
            }

            var reasons = root.TryGetProperty("reasons", out var r) && r.ValueKind == JsonValueKind.Array
                ? string.Join("; ", r.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)))
                : null;

            var detail = string.IsNullOrEmpty(reasons) ? $"status {code}" : $"status {code}: {reasons}";
            return new DomainCheckResult(domain, status, "registro.br", expires, detail, json);
        }
        catch (JsonException)
        {
            return new DomainCheckResult(domain, DomainStatus.Unknown, "registro.br", Detail: "Answer is not JSON.", Raw: json);
        }
    }
}
