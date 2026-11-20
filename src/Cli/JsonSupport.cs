using System.Text.Json;
namespace Tooling;

internal static class JsonSupport
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Parse<T>(string json) where T : class
    {
        using var doc = JsonDocument.Parse(json);
        CheckDuplicateProperties(doc.RootElement);
        return doc.RootElement.Deserialize<T>(Options) ?? throw new JsonException();
    }
    private static void CheckDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!keys.Add(property.Name)) throw new JsonException();
                CheckDuplicateProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) CheckDuplicateProperties(item);
    }
    public static List<T> Read<T>(string path) where T : class
    {
        var rows = new List<T>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            try { rows.Add(Parse<T>(line)); }
            catch (JsonException) { throw new InputException($"Invalid JSON record at line {lineNumber} in {path}."); }
        }
        if (rows.Count == 0) throw new InputException($"Dataset is empty: {path}.");
        return rows;
    }
}
