# DomainCheck

[![CI](https://github.com/CaiqueDourado/DomainCheck/actions/workflows/ci.yml/badge.svg)](https://github.com/CaiqueDourado/DomainCheck/actions/workflows/ci.yml)
[![Licença: MIT](https://img.shields.io/badge/licen%C3%A7a-MIT-blue.svg)](LICENSE)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![Dependências: 0](https://img.shields.io/badge/depend%C3%AAncias-0-brightgreen)

**Esse domínio está livre?** Para `.br` pergunta ao Registro.br; para `.com`, `.net`, `.io` e qualquer outra extensão pergunta ao WHOIS do registro, descoberto pela IANA. Um teste de DNS responde na hora os casos óbvios, e um gerador transforma palavras-chave em nomes candidatos.

[Read in English](README.md)

```text
$ domaincheck --words "casa das panelas"      # saída ilustrativa
domain                               status       expires     source
casadaspanelas.com.br                Registered   2027-05-18  registro.br
casa-das-panelas.com.br              Available    -           registro.br
casa-das-panelas.com                 Available    -           whois:whois.verisign-grs.com
...
```

## Como decide

1. **Normaliza**: `https://www.Loja.com.br/x` vira `loja.com.br`; nomes com acento viram punycode (`açaí.com.br` → `xn--aa-4iaz.com.br`).
2. **DNS primeiro**: se o nome resolve, está registrado, sem precisar consultar o registro. Os que não resolvem seguem para o registro, porque muitos domínios registrados não têm DNS.
3. **Registro**:
   - `.br` → endpoint de disponibilidade do Registro.br. Status `0` = livre, `2` = registrado (com data de expiração), `3` = inválido (com os motivos). Nomes com mais de 26 caracteres são recusados localmente, sem consulta.
   - demais extensões → WHOIS na porta 43. O servidor de cada extensão vem do `whois.iana.org` e fica em cache; os mais comuns (`.com`, `.net`, `.org`, `.br`) já vêm embutidos.
4. **Educado**: por padrão, pelo menos 1 segundo entre consultas ao mesmo servidor e poucas consultas em paralelo.

Todo resultado diz quem respondeu (`dns`, `registro.br`, `whois:<servidor>`) e guarda a resposta original.

## Biblioteca

```csharp
using var checker = DomainChecker.CreateDefault();

var r = await checker.CheckAsync("minhaloja.com.br");
Console.WriteLine($"{r.Domain}: {r.Status} via {r.Source}");

foreach (var nome in NameGenerator.FromWords("Casa das Panelas"))
    Console.WriteLine(nome);   // casadaspanelas.com.br, casa-das-panelas.com.br, casapanelas.com.br...
```

## Bom saber

- **Endpoint do Registro.br.** A consulta `.br` usa o endpoint da busca de disponibilidade do site registro.br. Não é uma API pública documentada e pode mudar; por isso a URL e o mapeamento de status são configuráveis. Mantenha volumes moderados.
- **"Livre" é um forte indício, não uma reserva.** A única garantia é registrar.
- **Por que não RDAP?** O RDAP é o sucessor padronizado do WHOIS, mas os servidores públicos (inclusive o do Registro.br) têm limites de consulta apertados, que inviabilizam checagens em volume. Na prática, o endpoint do Registro.br e o WHOIS na porta 43 aguentam muito mais. Um provedor RDAP opcional está nos próximos passos, e já dá para plugar um hoje via `IAvailabilityProvider`.
- **Cada registro tem seu formato de WHOIS.** Respostas não reconhecidas voltam como `Unknown` com o texto original — abra uma issue com o exemplo.

## Próximos passos

- Provedor RDAP opcional (como alternativa para extensões sem servidor WHOIS).
- Cache persistente de resultados, para retomar listas longas.

## Licença

[MIT](LICENSE) © Caique Dourado
