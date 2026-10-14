# Contributing

Thanks for helping! Issues and pull requests are welcome in English or Portuguese.

```bash
git clone https://github.com/CaiqueDourado/DomainCheck.git
cd DomainCheck
dotnet test
```

## Most useful contributions

- **An extension whose WHOIS answer is not recognised** (status `Unknown`): add a shortened, anonymised sample to `tests/DomainCheck.Tests/Fakes.cs` and adjust the patterns in `WhoisProvider`.
- **A new Registro.br status code** seen in practice: add it to `RegistroBrOptions.StatusMap` with a test.

## Guidelines

- No third-party dependencies in the library.
- Tests never touch the network: use `FakeHttp`, `FakeWhoisClient` and `FakeDns`.
- Keep the default rate limits conservative. Do not add proxy rotation or anything meant to evade registry limits.
- Public members need XML documentation; the build treats warnings as errors.
