using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DomainCheck;
using DomainCheck.Providers;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var domains = new List<string>();
var words = new List<string>();
var extensions = new List<string>();
var json = false;
var noDns = false;
var showAll = true;
int? delayMs = null;
int? concurrency = null;

try
{
    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "-h" or "--help":
                return Help();
            case "--version":
                Console.WriteLine(typeof(DomainChecker).Assembly.GetName().Version?.ToString(3));
                return 0;
            case "--json":
                json = true;
                break;
            case "--no-dns":
                noDns = true;
                break;
            case "--available":
                showAll = false;
                break;
            case "--words" or "-w":
                words.Add(Next(ref i));
                break;
            case "--tlds" or "-t":
                extensions.AddRange(Next(ref i).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                break;
            case "--file" or "-f":
                domains.AddRange(File.ReadAllLines(Next(ref i)).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')));
                break;
            case "--delay":
                delayMs = PositiveInt(Next(ref i), arg);
                break;
            case "--concurrency":
                concurrency = PositiveInt(Next(ref i), arg);
                break;
            default:
                if (arg.StartsWith('-'))
                {
                    throw new ArgumentException($"Unknown option '{arg}'.");
                }

                domains.Add(arg);
                break;
        }
    }

    string Next(ref int i) => ++i < args.Length ? args[i] : throw new ArgumentException($"{args[i - 1]} needs a value.");

    var generator = new NameGeneratorOptions();
    if (extensions.Count > 0)
    {
        generator.Extensions = extensions;
    }

    foreach (var phrase in words)
    {
        domains.AddRange(NameGenerator.FromWords(phrase, generator));
    }

    if (domains.Count == 0)
    {
        return Help();
    }

    var interval = delayMs is null ? (TimeSpan?)null : TimeSpan.FromMilliseconds(delayMs.Value);
    var registro = new RegistroBrOptions();
    var whois = new WhoisOptions();
    if (interval is { } delay)
    {
        registro.MinimumInterval = delay;
        whois.MinimumIntervalPerServer = delay;
    }

    using var checker = DomainChecker.CreateDefault(
        new DomainCheckerOptions { UseDnsPrecheck = !noDns, MaxConcurrency = concurrency ?? 4 },
        registro,
        whois);

    var results = new List<DomainCheckResult>();
    if (!json)
    {
        Console.WriteLine($"{"domain",-36} {"status",-12} {"expires",-11} source");
    }

    await foreach (var result in checker.CheckManyAsync(domains))
    {
        if (!showAll && !result.IsAvailable)
        {
            continue;
        }

        results.Add(result);
        if (!json)
        {
            var expires = result.ExpiresAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-";
            var detail = result.Status is DomainStatus.Unknown or DomainStatus.Invalid && result.Detail is not null ? "  " + result.Detail : string.Empty;
            Console.WriteLine($"{result.Domain,-36} {result.Status,-12} {expires,-11} {result.Source}{detail}");
        }
    }

    if (json)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() },
        };
        Console.WriteLine(JsonSerializer.Serialize(results.Select(r => r with { Raw = null }), options));
    }

    return results.Any(r => r.Status == DomainStatus.Unknown) ? 3 : 0;
}
catch (Exception ex) when (ex is ArgumentException or IOException)
{
    Console.Error.WriteLine("Error: " + ex.Message);
    return 2;
}

static int PositiveInt(string value, string option) =>
    int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0
        ? n
        : throw new ArgumentException($"{option} must be a positive integer.");

static int Help()
{
    Console.WriteLine("""
        domaincheck - domain availability (.br via Registro.br, others via WHOIS)

        Usage:
          domaincheck <domain>... [options]
          domaincheck --words "casa das panelas" [--tlds com.br,com,net] [options]

        Options:
          -w, --words <text>     Generate candidates from keywords (repeatable)
          -t, --tlds <list>      Extensions for --words (default: com.br,com)
          -f, --file <path>      Read domains from a file, one per line
              --available        Show only available domains
              --json             JSON output
              --no-dns           Skip the DNS pre-check
              --delay <ms>       Minimum interval between queries to the same server (default: 1000)
              --concurrency <n>  Domains checked in parallel (default: 4)

        Exit code: 0 = all answered, 3 = some results are Unknown, 2 = usage error.
        """);
    return 0;
}
