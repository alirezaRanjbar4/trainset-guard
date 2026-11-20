using System.Globalization;
namespace Tooling;

public sealed class InputException(string message) : Exception(message);
internal sealed class Options
{
    private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
    public Options(IEnumerable<string> arguments, params string[] allowed)
    {
        var items = arguments.ToArray();
        for (var i = 0; i < items.Length; i++)
        {
            if (!allowed.Contains(items[i], StringComparer.Ordinal))
                throw new InputException("Unknown argument. See --help.");
            var key = items[i];
            if (i + 1 >= items.Length || items[i + 1].StartsWith("--", StringComparison.Ordinal))
                throw new InputException($"Missing value for {key}.");
            if (!values.TryAdd(key, items[++i]))
                throw new InputException($"Option {key} was repeated.");
        }
    }
    public string Require(string key) => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value : throw new InputException($"Required option: {key}.");
    public string? Get(string key) => values.GetValueOrDefault(key);
    public int Int(string key, int fallback) => Get(key) is not { } raw ? fallback :
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value :
        throw new InputException($"Expected an integer for {key}.");
    public double Fraction(string key, double fallback) => Get(key) is not { } raw ? fallback :
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) && value is >= 0 and <= 1
            ? value : throw new InputException($"Expected a number between 0 and 1 for {key}.");
}
