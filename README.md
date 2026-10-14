# DomainCheck

[![CI](https://github.com/CaiqueDourado/DomainCheck/actions/workflows/ci.yml/badge.svg)](https://github.com/CaiqueDourado/DomainCheck/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![Dependencies: 0](https://img.shields.io/badge/dependencies-0-brightgreen)

**Is this domain available?** For `.br` it asks Registro.br; for `.com`, `.net`, `.io` and every other extension it asks the registry's WHOIS server, discovered through IANA. A DNS pre-check answers the obvious cases instantly, and a name generator turns keywords into candidates.

[Leia em português](README.pt-BR.md)

```text
$ domaincheck --words "casa das panelas"      # illustrative output
domain                               status       expires     source
casadaspanelas.com.br                Registered   2027-05-18  registro.br
casadaspanelas.com                   Registered   -           dns
casa-das-panelas.com.br              Available    -           registro.br
casa-das-panelas.com                 Available    -           whois:whois.verisign-grs.com
casapanelas.com.br                   Available    -           registro.br
...
```

## How it decides

1. **Normalise**: `https://www.Loja.com.br/x` → `loja.com.br`; internationalised names to punycode (`açaí.com.br` → `xn--aa-4iaz.com.br`).
2. **DNS pre-check**: a name that resolves is registered — no registry query needed. Names that do not resolve still go to the registry, because many registered domains have no DNS.
3. **Registry**:
   - `.br` → Registro.br availability endpoint. Status `0` = available, `2` = registered (with expiry date), `3` = invalid (with the reasons). Names longer than 26 characters are rejected locally, without a request.
   - everything else → WHOIS on port 43. The server of each extension comes from `whois.iana.org` and is cached; well-known ones (`.com`, `.net`, `.org`, `.br`) are built in.
4. **Be polite**: at least 1 second between queries to the same server by default, and a few domains in parallel at most.

Every result says who answered (`dns`, `registro.br`, `whois:<server>`) and keeps the raw answer for auditing.

## Install

```bash
git clone https://github.com/CaiqueDourado/DomainCheck.git
cd DomainCheck
dotnet run --project src/DomainCheck.Cli -- loja.com.br loja.com
```

Each release attaches the `.nupkg` packages and ready-to-run CLI builds (Windows, Linux, macOS) to the [Releases](https://github.com/CaiqueDourado/DomainCheck/releases) page.

## Library

```csharp
using DomainCheck;

using var checker = DomainChecker.CreateDefault();

var one = await checker.CheckAsync("minhaloja.com.br");
Console.WriteLine($"{one.Domain}: {one.Status} via {one.Source}, expires {one.ExpiresAt:yyyy-MM-dd}");

var candidates = NameGenerator.FromWords("Casa das Panelas", new NameGeneratorOptions { Extensions = ["com.br", "com", "net"] });
await foreach (var result in checker.CheckManyAsync(candidates))
{
    if (result.IsAvailable) Console.WriteLine(result.Domain);
}
```

Customise when needed:

```csharp
var registro = new RegistroBrOptions { MinimumInterval = TimeSpan.FromSeconds(2) };
registro.StatusMap[7] = DomainStatus.Unavailable;              // map codes you meet in practice

var whois = new WhoisOptions();
whois.Servers["app"] = "whois.nic.google";                     // skip IANA discovery for an extension

using var checker = DomainChecker.CreateDefault(new DomainCheckerOptions { MaxConcurrency = 2 }, registro, whois);
```

Providers are pluggable (`IAvailabilityProvider`), as are the WHOIS transport (`IWhoisClient`) and the DNS resolver (`IDnsResolver`) — which is also how the tests run without network.

## CLI

```text
domaincheck <domain>... [options]
domaincheck --words "casa das panelas" [--tlds com.br,com,net] [options]

  -w, --words <text>     Generate candidates from keywords (repeatable)
  -t, --tlds <list>      Extensions for --words (default: com.br,com)
  -f, --file <path>      Read domains from a file, one per line
      --available        Show only available domains
      --json             JSON output
      --no-dns           Skip the DNS pre-check
      --delay <ms>       Minimum interval between queries to the same server (default: 1000)
      --concurrency <n>  Domains checked in parallel (default: 4)
```

## Good to know

- **Registro.br endpoint.** The `.br` check uses the endpoint behind the availability search on registro.br. It is not a documented public API and may change; the URL and the status mapping are configurable for that reason. Keep volumes moderate.
- **"Available" is a strong hint, not a reservation.** The only proof is registering the domain. Premium and reserved names may show as available in WHOIS.
- **Why not RDAP?** RDAP is the standard successor of WHOIS, but its public servers (including Registro.br's) apply tight rate limits that make bulk checks impractical. The Registro.br endpoint and port-43 WHOIS handle volume much better in practice. An optional RDAP provider is on the roadmap, and you can plug one in today through `IAvailabilityProvider`.
- **WHOIS formats vary by registry.** Unrecognised answers come back as `Unknown` with the raw text — please open an issue with a sample.

## Roadmap

- Optional RDAP provider (as a fallback for extensions without a WHOIS server).
- Persisted cache of results, to resume long lists.

## License

[MIT](LICENSE) © Caique Dourado
