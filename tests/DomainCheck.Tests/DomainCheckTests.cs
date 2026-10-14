using System.Net;
using DomainCheck.Providers;

namespace DomainCheck.Tests;

public class DomainNameTests
{
    [Theory]
    [InlineData("Loja.com.br", "loja.com.br")]
    [InlineData("https://www.Loja.com.br/produtos?x=1", "loja.com.br")]
    [InlineData("loja.com.br.", "loja.com.br")]
    [InlineData("loja.com.br:443", "loja.com.br")]
    [InlineData("www.com.br", "www.com.br")]
    [InlineData("açaí.com.br", "xn--aa-4iaz.com.br")]
    [InlineData("pão.com", "xn--po-sia.com")]
    public void Normalizes(string input, string expected) => Assert.Equal(expected, DomainName.Normalize(input));

    [Theory]
    [InlineData("")]
    [InlineData("loja")]
    [InlineData("-loja.com.br")]
    [InlineData("loja-.com.br")]
    [InlineData("lo ja.com.br")]
    [InlineData("loja_1.com")]
    public void Rejects_invalid(string input) => Assert.Null(DomainName.Normalize(input));

    [Theory]
    [InlineData("loja.com.br", "com.br")]
    [InlineData("loja.net.br", "net.br")]
    [InlineData("loja.com", "com")]
    [InlineData("loja.io", "io")]
    public void Extension(string domain, string expected) => Assert.Equal(expected, DomainName.Extension(domain));
}

public class NameGeneratorTests
{
    [Fact]
    public void Generates_variants_with_and_without_connectors()
    {
        var names = NameGenerator.FromWords("Casa das Panelas");

        Assert.Equal(new[]
        {
            "casadaspanelas.com.br", "casadaspanelas.com",
            "casa-das-panelas.com.br", "casa-das-panelas.com",
            "casapanelas.com.br", "casapanelas.com",
            "casa-panelas.com.br", "casa-panelas.com",
        }, names);
    }

    [Fact]
    public void Removes_accents_and_symbols()
    {
        var names = NameGenerator.FromWords("Açaí & Cia!", new NameGeneratorOptions { Extensions = ["com.br"], IncludeHyphenated = false });
        Assert.Equal(new[] { "acaicia.com.br" }, names);
    }

    [Fact]
    public void Single_word()
    {
        var names = NameGenerator.FromWords("Panelas", new NameGeneratorOptions { Extensions = ["com.br", ".net"] });
        Assert.Equal(new[] { "panelas.com.br", "panelas.net" }, names);
    }

    [Fact]
    public void Empty_input() => Assert.Empty(NameGenerator.FromWords("  !!  "));
}

public class RegistroBrProviderTests
{
    [Fact]
    public void Available()
    {
        var result = RegistroBrProvider.Parse("caiquelibtest2026.com.br", Samples.RegistroBrAvailable);

        Assert.Equal(DomainStatus.Available, result.Status);
        Assert.True(result.IsAvailable);
        Assert.Equal("registro.br", result.Source);
        Assert.Null(result.ExpiresAt);
    }

    [Fact]
    public void Registered_with_expiry()
    {
        var result = RegistroBrProvider.Parse("google.com.br", Samples.RegistroBrRegistered);

        Assert.Equal(DomainStatus.Registered, result.Status);
        Assert.Equal(new DateTimeOffset(2027, 5, 18, 0, 0, 0, TimeSpan.FromHours(-3)), result.ExpiresAt);
    }

    [Fact]
    public void Invalid_with_reasons()
    {
        var result = RegistroBrProvider.Parse("algumacoisaquenaoexiste12345.com.br", Samples.RegistroBrInvalid);

        Assert.Equal(DomainStatus.Invalid, result.Status);
        Assert.Contains("tamanho máximo de 26 caracteres", result.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_status_keeps_the_code()
    {
        var result = RegistroBrProvider.Parse("x.com.br", """{"status":7,"fqdn":"x.com.br"}""");

        Assert.Equal(DomainStatus.Unknown, result.Status);
        Assert.Equal("status 7", result.Detail);
    }

    [Fact]
    public void Status_map_can_be_extended()
    {
        var options = new RegistroBrOptions();
        options.StatusMap[7] = DomainStatus.Unavailable;

        Assert.Equal(DomainStatus.Unavailable, RegistroBrProvider.Parse("x.com.br", """{"status":7}""", options.StatusMap).Status);
    }

    [Fact]
    public void Not_json() => Assert.Equal(DomainStatus.Unknown, RegistroBrProvider.Parse("x.com.br", "<html>").Status);

    [Fact]
    public async Task Calls_the_endpoint()
    {
        var http = new FakeHttp(_ => FakeHttp.Json(Samples.RegistroBrAvailable));
        var provider = new RegistroBrProvider(new HttpClient(http), new RegistroBrOptions { MinimumInterval = TimeSpan.Zero });

        var result = await provider.CheckAsync("caiquelibtest2026.com.br");

        Assert.Equal(DomainStatus.Available, result.Status);
        Assert.Equal("https://registro.br/v2/ajax/avail/raw/caiquelibtest2026.com.br", Assert.Single(http.Requests).ToString());
    }

    [Fact]
    public async Task Long_names_are_rejected_without_a_request()
    {
        var http = new FakeHttp(_ => throw new InvalidOperationException("must not be called"));
        var provider = new RegistroBrProvider(new HttpClient(http));

        var result = await provider.CheckAsync("algumacoisaquenaoexiste12345.com.br");

        Assert.Equal(DomainStatus.Invalid, result.Status);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task Http_error_is_unknown()
    {
        var http = new FakeHttp(_ => FakeHttp.Json("{}", HttpStatusCode.TooManyRequests));
        var provider = new RegistroBrProvider(new HttpClient(http), new RegistroBrOptions { MinimumInterval = TimeSpan.Zero });

        var result = await provider.CheckAsync("loja.com.br");

        Assert.Equal(DomainStatus.Unknown, result.Status);
        Assert.Equal("HTTP 429", result.Detail);
    }

    [Fact]
    public async Task Respects_minimum_interval()
    {
        var http = new FakeHttp(_ => FakeHttp.Json(Samples.RegistroBrAvailable));
        var provider = new RegistroBrProvider(new HttpClient(http), new RegistroBrOptions { MinimumInterval = TimeSpan.FromMilliseconds(200) });

        var watch = System.Diagnostics.Stopwatch.StartNew();
        await provider.CheckAsync("aa.com.br");
        await provider.CheckAsync("bb.com.br");
        await provider.CheckAsync("cc.com.br");

        Assert.True(watch.ElapsedMilliseconds >= 380, $"took {watch.ElapsedMilliseconds} ms");
    }
}

public class WhoisProviderTests
{
    private static WhoisOptions Fast() => new() { MinimumIntervalPerServer = TimeSpan.Zero };

    [Fact]
    public void Parses_registered_with_expiry()
    {
        var result = WhoisProvider.Parse("exemplo-registrado.com", Samples.VerisignRegistered);

        Assert.Equal(DomainStatus.Registered, result.Status);
        Assert.Equal(new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero), result.ExpiresAt);
    }

    [Theory]
    [InlineData(Samples.VerisignNotFound)]
    [InlineData(Samples.NicIoNotFound)]
    [InlineData(Samples.RegistroBrWhoisNotFound)]
    public void Parses_not_found(string answer) =>
        Assert.Equal(DomainStatus.Available, WhoisProvider.Parse("x.com", answer).Status);

    [Fact]
    public void Rate_limit_is_unknown()
    {
        var result = WhoisProvider.Parse("x.org", Samples.RateLimited);
        Assert.Equal(DomainStatus.Unknown, result.Status);
        Assert.Equal("Rate limited by the WHOIS server.", result.Detail);
    }

    [Fact]
    public async Task Uses_known_server_for_com()
    {
        var client = new FakeWhoisClient(new() { [("whois.verisign-grs.com", "exemplo-livre-2026.com")] = Samples.VerisignNotFound });
        var provider = new WhoisProvider(client, Fast());

        var result = await provider.CheckAsync("exemplo-livre-2026.com");

        Assert.Equal(DomainStatus.Available, result.Status);
        Assert.Equal("whois:whois.verisign-grs.com", result.Source);
    }

    [Fact]
    public async Task Discovers_server_from_iana_and_caches_it()
    {
        var client = new FakeWhoisClient(new()
        {
            [("whois.iana.org", "io")] = Samples.IanaIo,
            [("whois.nic.io", "a.io")] = Samples.NicIoNotFound,
            [("whois.nic.io", "b.io")] = Samples.NicIoNotFound,
        });
        var provider = new WhoisProvider(client, Fast());

        await provider.CheckAsync("a.io");
        var result = await provider.CheckAsync("b.io");

        Assert.Equal(DomainStatus.Available, result.Status);
        Assert.Equal(1, client.Calls.Count(c => c.Server == "whois.iana.org"));
    }

    [Fact]
    public async Task Network_error_is_unknown()
    {
        var provider = new WhoisProvider(new FakeWhoisClient([]), Fast());
        var result = await provider.CheckAsync("x.com");
        Assert.Equal(DomainStatus.Unknown, result.Status);
    }
}

public class DomainCheckerTests
{
    private static DomainChecker Checker(FakeDns dns, out FakeHttp http)
    {
        var handler = new FakeHttp(request => request.RequestUri!.AbsolutePath.EndsWith("google.com.br", StringComparison.Ordinal)
            ? FakeHttp.Json(Samples.RegistroBrRegistered)
            : FakeHttp.Json(Samples.RegistroBrAvailable));
        http = handler;

        var whois = new WhoisProvider(
            new FakeWhoisClient(new() { [("whois.verisign-grs.com", "exemplo-livre-2026.com")] = Samples.VerisignNotFound }),
            new WhoisOptions { MinimumIntervalPerServer = TimeSpan.Zero });

        return new DomainChecker(
            [new RegistroBrProvider(new HttpClient(handler), new RegistroBrOptions { MinimumInterval = TimeSpan.Zero }), whois],
            dns);
    }

    [Fact]
    public async Task Resolving_name_is_registered_without_asking_the_registry()
    {
        using var checker = Checker(new FakeDns(new() { ["loja.com.br"] = true }), out var http);

        var result = await checker.CheckAsync("https://www.loja.com.br/");

        Assert.Equal(new DomainCheckResult("loja.com.br", DomainStatus.Registered, "dns", Detail: "The name resolves."), result);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task Br_goes_to_registro_br_and_others_to_whois()
    {
        using var checker = Checker(new FakeDns([]), out _);

        Assert.Equal("registro.br", (await checker.CheckAsync("caiquelibtest2026.com.br")).Source);
        Assert.Equal("whois:whois.verisign-grs.com", (await checker.CheckAsync("exemplo-livre-2026.com")).Source);
    }

    [Fact]
    public async Task Invalid_input()
    {
        using var checker = Checker(new FakeDns([]), out _);
        var result = await checker.CheckAsync("isso não é domínio");
        Assert.Equal(DomainStatus.Invalid, result.Status);
        Assert.Equal("syntax", result.Source);
    }

    [Fact]
    public async Task Check_many_removes_duplicates()
    {
        using var checker = Checker(new FakeDns([]), out var http);

        var results = new List<DomainCheckResult>();
        await foreach (var r in checker.CheckManyAsync(["google.com.br", "GOOGLE.com.br", "caiquelibtest2026.com.br"]))
        {
            results.Add(r);
        }

        Assert.Equal(2, results.Count);
        Assert.Equal(2, http.Requests.Count);
        Assert.Contains(results, r => r.Domain == "google.com.br" && r.Status == DomainStatus.Registered);
        Assert.Contains(results, r => r.Domain == "caiquelibtest2026.com.br" && r.IsAvailable);
    }
}
