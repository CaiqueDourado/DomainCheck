using System.Net;
using System.Text;
using DomainCheck.Providers;

namespace DomainCheck.Tests;

internal static class Samples
{
    // Real answers from https://registro.br/v2/ajax/avail/raw/{domain}, October 2026.
    public const string RegistroBrAvailable =
        """{"status":0,"fqdn":"caiquelibtest2026.com.br","fqdnace":"","exempt":false}""";

    public const string RegistroBrRegistered =
        """{"status":2,"fqdn":"google.com.br","fqdnace":"","exempt":false,"hosts":["ns1.google.com","ns2.google.com","ns3.google.com","ns4.google.com"],"publication-status":"published","expires-at":"2027-05-18T00:00:00-03:00","suggestions":["agr.br","ong.br","tmp.br"]}""";

    public const string RegistroBrInvalid =
        """{"status":3,"fqdn":"algumacoisaquenaoexiste12345.com.br","fqdnace":"","exempt":false,"reasons":["Domínio inválido","Domínio inválido, tamanho máximo de 26 caracteres"]}""";

    // WHOIS answers in the shape returned by the registries (shortened, values invented).
    public const string VerisignRegistered = """
           Domain Name: EXEMPLO-REGISTRADO.COM
           Registry Domain ID: 123456789_DOMAIN_COM-VRSN
           Registrar WHOIS Server: whois.example-registrar.com
           Updated Date: 2026-01-10T10:00:00Z
           Creation Date: 2010-03-01T12:00:00Z
           Registry Expiry Date: 2027-03-01T12:00:00Z
           Registrar: Example Registrar, Inc.
        """;

    public const string VerisignNotFound = """
        No match for "EXEMPLO-LIVRE-2026.COM".
        >>> Last update of whois database: 2026-10-14T18:00:00Z <<<
        """;

    public const string IanaIo = """
        % IANA WHOIS server
        % for more information on IANA, visit http://www.iana.org

        domain:       IO

        organisation: Internet Computer Bureau Limited
        whois:        whois.nic.io

        status:       ACTIVE
        """;

    public const string NicIoNotFound = "Domain not found.\r\n";

    public const string RegistroBrWhoisNotFound = "% Copyright (c) Nic.br\r\n% No match for domain \"exemplo-livre.com.br\"\r\n";

    public const string RateLimited = "WHOIS LIMIT EXCEEDED - SEE WWW.EXAMPLE.ORG/WHOIS\r\n";
}

internal sealed class FakeWhoisClient(Dictionary<(string Server, string Query), string> answers) : IWhoisClient
{
    public List<(string Server, string Query)> Calls { get; } = [];

    public Task<string> QueryAsync(string server, string query, CancellationToken cancellationToken = default)
    {
        Calls.Add((server, query));
        return answers.TryGetValue((server, query), out var answer)
            ? Task.FromResult(answer)
            : throw new IOException($"No fake answer for {server} {query}");
    }
}

internal sealed class FakeDns(Dictionary<string, bool?> answers) : IDnsResolver
{
    public Task<bool?> ResolvesAsync(string host, CancellationToken cancellationToken = default) =>
        Task.FromResult(answers.TryGetValue(host, out var value) ? value : false);
}

internal sealed class FakeHttp(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(respond(request));
    }
}
