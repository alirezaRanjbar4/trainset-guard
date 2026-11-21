using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tooling;

namespace TrainSetGuard;

public sealed record SplitManifest(int SchemaVersion, string FingerprintPolicy, int Seed, double RequestedValidationFraction,
    double ActualValidationFraction, int SourceRows, int PromptGroups, int TrainRows, int ValidationRows,
    int TrainGroups, int ValidationGroups, string SourceSha256, string TrainSha256, string ValidationSha256);

public static class DatasetSplitter
{
    private sealed record Row(string Json, string Prompt);

    public static SplitManifest Split(string input, string trainOutput, string validationOutput, string manifestOutput,
        double validationFraction = 0.2, int seed = 42)
    {
        if (!double.IsFinite(validationFraction) || validationFraction is <= 0 or >= 1)
            throw new InputException("Validation fraction must be strictly between 0 and 1.");
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var paths = new[] { trainOutput, validationOutput, manifestOutput }.Select(Path.GetFullPath).ToArray();
        if (paths.Distinct(comparer).Count() != paths.Length || paths.Any(path => File.Exists(path) || Directory.Exists(path)))
            throw new InputException("Split outputs must be three distinct fresh file paths.");

        var source = File.ReadAllBytes(input);
        var rows = new List<Row>();
        var groups = new Dictionary<string, (string Answer, int FirstLine)>(StringComparer.Ordinal);
        using (var reader = new StreamReader(new MemoryStream(source), new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true))
        {
            while (reader.ReadLine() is { } line)
            {
                var number = rows.Count + 1;
                ChatExample example;
                try { example = JsonSupport.Parse<ChatExample>(line); }
                catch (JsonException) { throw new InputException($"Invalid JSON at input line {number}; no split was written."); }
                if (!DatasetGuard.ValidConversation(example.Messages))
                    throw new InputException($"Invalid conversation at input line {number}; no split was written.");
                var (prompt, answer) = DatasetGuard.Fingerprints(example);
                if (groups.TryGetValue(prompt, out var first) && first.Answer != answer)
                    throw new InputException($"Conflicting answers at input lines {first.FirstLine} and {number}; no split was written.");
                groups.TryAdd(prompt, (answer, number));
                rows.Add(new(line, prompt));
            }
        }
        if (groups.Count < 2) throw new InputException("At least two distinct prompt groups are required for nonempty splits.");
        var validationGroupCount = Math.Clamp((int)Math.Round(groups.Count * validationFraction, MidpointRounding.AwayFromZero), 1, groups.Count - 1);
        // Hash ranking is stable across .NET versions; row ordering is preserved within each output.
        var selected = groups.Keys.OrderBy(prompt => Hash(Encoding.UTF8.GetBytes(seed.ToString(CultureInfo.InvariantCulture) + ":" + prompt)), StringComparer.Ordinal)
            .ThenBy(prompt => prompt, StringComparer.Ordinal).Take(validationGroupCount).ToHashSet(StringComparer.Ordinal);
        var train = rows.Where(row => !selected.Contains(row.Prompt)).Select(row => row.Json).ToArray();
        var validation = rows.Where(row => selected.Contains(row.Prompt)).Select(row => row.Json).ToArray();
        var trainBytes = Encoding.UTF8.GetBytes(string.Join("\n", train) + "\n");
        var validationBytes = Encoding.UTF8.GetBytes(string.Join("\n", validation) + "\n");
        var report = new SplitManifest(1, "chat-nfkc-v1", seed, validationFraction, (double)validation.Length / rows.Count,
            rows.Count, groups.Count, train.Length, validation.Length, groups.Count - selected.Count, selected.Count,
            Hash(source), Hash(trainBytes), Hash(validationBytes));

        foreach (var path in paths) Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var created = new List<string>();
        try
        {
            WriteNew(paths[0], trainBytes);
            WriteNew(paths[1], validationBytes);
            WriteNew(paths[2], Encoding.UTF8.GetBytes(JsonSupport.Serialize(report) + "\n"));
        }
        catch
        {
            // Delete only files successfully created by this invocation, never pre-existing paths.
            foreach (var path in created)
                try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
        return report;

        void WriteNew(string path, byte[] bytes)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            created.Add(path);
            stream.Write(bytes);
        }
    }

    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
}
