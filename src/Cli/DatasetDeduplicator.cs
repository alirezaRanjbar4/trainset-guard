using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tooling;

namespace TrainSetGuard;

public sealed record DuplicateOccurrence(int Line, int KeptLine);
public sealed record DeduplicationReport(int SchemaVersion, string FingerprintPolicy, int SourceRows, int ValidExamples,
    int UniqueExamples, int DuplicateExamples, int ConflictingPromptGroups, string SourceSha256,
    IReadOnlyList<DuplicateOccurrence> Duplicates, IReadOnlyList<Finding> Diagnostics)
{
    public int InvalidExamples => SourceRows - ValidExamples;
    public bool CanExport => ValidExamples > 0 && Diagnostics.Count == 0;
    public bool HasFindings => DuplicateExamples > 0 || Diagnostics.Count > 0;
}
public sealed record DeduplicationManifest(int SchemaVersion, string FingerprintPolicy, int SourceRows, int OutputRows,
    int RemovedRows, string SourceSha256, string OutputSha256, IReadOnlyList<int> KeptSourceLines,
    IReadOnlyList<DuplicateOccurrence> RemovedDuplicates);

public static class DatasetDeduplicator
{
    private sealed record RetainedRow(int Line, string Json);
    private sealed record Inspection(DeduplicationReport Report, IReadOnlyList<RetainedRow> Rows);

    public static DeduplicationReport Preview(string input) => Inspect(input).Report;

    public static DeduplicationManifest Export(string input, string output, string manifestOutput)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var paths = new[] { output, manifestOutput }.Select(Path.GetFullPath).ToArray();
        if (paths.Distinct(comparer).Count() != paths.Length || paths.Any(path => File.Exists(path) || Directory.Exists(path)))
            throw new InputException("Deduplication outputs must be distinct fresh file paths.");
        var inspection = Inspect(input);
        if (!inspection.Report.CanExport)
            throw new InputException("Export refused: input has invalid records, conflicting answers, or no valid examples. Run dedup to inspect it; no outputs were written.");
        var bytes = Encoding.UTF8.GetBytes(string.Join("\n", inspection.Rows.Select(row => row.Json)) + "\n");
        var report = new DeduplicationManifest(1, "chat-nfkc-v1", inspection.Report.SourceRows, inspection.Rows.Count,
            inspection.Report.DuplicateExamples, inspection.Report.SourceSha256, Hash(bytes),
            inspection.Rows.Select(row => row.Line).ToArray(), inspection.Report.Duplicates);
        foreach (var path in paths) Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var created = new List<string>();
        try
        {
            WriteNew(paths[0], bytes);
            WriteNew(paths[1], Encoding.UTF8.GetBytes(JsonSupport.Serialize(report) + "\n"));
        }
        catch
        {
            // Roll back only files exclusively created by this call.
            foreach (var path in created)
                try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
        return report;

        void WriteNew(string path, byte[] content)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            created.Add(path);
            stream.Write(content);
        }
    }

    private static Inspection Inspect(string input)
    {
        var source = File.ReadAllBytes(input);
        var rows = new List<RetainedRow>();
        var duplicates = new List<DuplicateOccurrence>();
        var diagnostics = new List<Finding>();
        var pairs = new Dictionary<(string Prompt, string Answer), int>();
        var firstAnswers = new Dictionary<string, (string Answer, int Line)>(StringComparer.Ordinal);
        var conflicts = new HashSet<string>(StringComparer.Ordinal);
        var total = 0;
        var valid = 0;
        using var reader = new StreamReader(new MemoryStream(source), new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        while (reader.ReadLine() is { } line)
        {
            total++;
            ChatExample example;
            try { example = JsonSupport.Parse<ChatExample>(line); }
            catch (JsonException)
            {
                diagnostics.Add(new("TSG001", input, total, null, "Invalid JSON object or duplicate JSON properties."));
                continue;
            }
            if (!DatasetGuard.ValidConversation(example.Messages))
            {
                diagnostics.Add(new("TSG002", input, total, null, "Expected a supported text-only conversation."));
                continue;
            }
            valid++;
            var pair = DatasetGuard.Fingerprints(example);
            if (pairs.TryGetValue(pair, out var firstLine))
            {
                duplicates.Add(new(total, firstLine));
                continue;
            }
            pairs.Add(pair, total);
            rows.Add(new(total, line));
            if (firstAnswers.TryGetValue(pair.Prompt, out var first) && first.Answer != pair.Answer)
            {
                conflicts.Add(pair.Prompt);
                diagnostics.Add(new("TSG004", input, total, first.Line, "Same normalized prompt has a conflicting final answer."));
            }
            else firstAnswers.TryAdd(pair.Prompt, (pair.Answer, total));
        }
        if (total == 0) diagnostics.Add(new("TSG006", input, 0, null, "Dataset is empty."));
        return new(new(1, "chat-nfkc-v1", total, valid, rows.Count, duplicates.Count, conflicts.Count,
            Hash(source), duplicates, diagnostics), rows);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
