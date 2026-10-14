using System.Text;

namespace DomainCheck;

/// <summary>Options for <see cref="NameGenerator"/>.</summary>
public sealed class NameGeneratorOptions
{
    /// <summary>Extensions to combine with each name. Default: <c>com.br</c> and <c>com</c>.</summary>
    public IReadOnlyList<string> Extensions { get; set; } = ["com.br", "com"];

    /// <summary>Also generate the hyphenated form (<c>casa-das-panelas</c>). Default: true.</summary>
    public bool IncludeHyphenated { get; set; } = true;

    /// <summary>Also generate forms without Portuguese connectors (<c>casapanelas</c>). Default: true.</summary>
    public bool IncludeWithoutConnectors { get; set; } = true;
}

/// <summary>
/// Turns keywords into candidate domain names: <c>"Casa das Panelas"</c> →
/// <c>casadaspanelas.com.br</c>, <c>casa-das-panelas.com.br</c>, <c>casapanelas.com.br</c>, ... .
/// </summary>
public static class NameGenerator
{
    private static readonly HashSet<string> Connectors = new(StringComparer.Ordinal)
    {
        "a", "o", "as", "os", "de", "da", "do", "das", "dos", "e", "em", "na", "no", "nas", "nos", "para", "pra", "com", "the", "of", "and",
    };

    private const string Accented = "àáâãäåçèéêëìíîïñòóôõöùúûüýÿ";
    private const string Plain = "aaaaaaceeeeiiiinooooouuuuyy";

    /// <summary>Generates candidates for <paramref name="keywords"/>, without duplicates, in a stable order.</summary>
    /// <param name="keywords">Words or a phrase, any case, accents allowed.</param>
    /// <param name="options">Options; null = defaults.</param>
    public static IReadOnlyList<string> FromWords(string keywords, NameGeneratorOptions? options = null)
    {
        options ??= new NameGeneratorOptions();
        var words = Words(keywords);
        if (words.Count == 0)
        {
            return [];
        }

        var names = new List<string> { string.Concat(words) };
        if (options.IncludeHyphenated && words.Count > 1)
        {
            names.Add(string.Join('-', words));
        }

        var core = words.Where(w => !Connectors.Contains(w)).ToList();
        if (options.IncludeWithoutConnectors && core.Count > 0 && core.Count < words.Count)
        {
            names.Add(string.Concat(core));
            if (options.IncludeHyphenated && core.Count > 1)
            {
                names.Add(string.Join('-', core));
            }
        }

        return names
            .Distinct(StringComparer.Ordinal)
            .Where(n => n.Length <= 63)
            .SelectMany(n => options.Extensions.Select(ext => n + "." + ext.TrimStart('.')))
            .ToList();
    }

    private static List<string> Words(string text)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        foreach (var raw in text.ToLowerInvariant())
        {
            var index = Accented.IndexOf(raw, StringComparison.Ordinal);
            var c = index >= 0 ? Plain[index] : raw;
            if (char.IsAsciiLetterOrDigit(c))
            {
                current.Append(c);
            }
            else if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            words.Add(current.ToString());
        }

        return words;
    }
}
