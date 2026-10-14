# Changelog

All notable changes to this project are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [0.1.0] - 2026-10-14

### Added

- `DomainChecker` with DNS pre-check, provider routing and concurrent `CheckManyAsync`.
- `RegistroBrProvider` for `.br` (status 0 available, 2 registered with expiry, 3 invalid with reasons; configurable status map; local 2–26 character check; 1 s minimum interval).
- `WhoisProvider` over TCP port 43 for every other extension, with WHOIS server discovery through IANA, caching, per-server rate limiting and expiry date parsing.
- `DomainName` normalisation (scheme, path, `www.`, IDN/punycode) and `NameGenerator` (candidates from keywords, with and without hyphens and Portuguese connectors).
- `domaincheck` command-line tool.
