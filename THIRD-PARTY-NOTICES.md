# Third-party notices

DomainCheck contains no third-party source code and has no runtime dependencies other than the .NET base class library (MIT, © .NET Foundation and Contributors).

External services queried at run time (not bundled):

- **Registro.br** — `https://registro.br/v2/ajax/avail/raw/{domain}`, the availability endpoint used by the Registro.br website. It is not a documented public API; it may change or be rate limited. Use moderate volumes.
- **IANA WHOIS** (`whois.iana.org`) to discover the WHOIS server of each extension, and the WHOIS servers of each registry. Their terms of use apply.

Test-only packages (not redistributed): xUnit (Apache-2.0), Microsoft.NET.Test.Sdk (MIT), coverlet.collector (MIT).
