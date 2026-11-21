using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tooling;
using TrainSetGuard;

namespace Tests;

public sealed class DeduplicationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "trainset-dedup-tests-" + Guid.NewGuid().ToString("N"));
    private string PathOf(string name) { Directory.CreateDirectory(root); return Path.Combine(root, name); }
    private string Source(params string[] lines)
    {
        var path = PathOf(Guid.NewGuid().ToString("N") + ".jsonl");
        File.WriteAllLines(path, lines);
        return path;
    }
    private static string Chat(string prompt, string answer = "answer", string metadata = "first") => JsonSerializer.Serialize(new
    {
        messages = new[] { new { role = "user", content = prompt }, new { role = "assistant", content = answer } },
        metadata
    });

    [Fact]
    public void PreviewCountsNormalizedDuplicatesWithoutChangingTheSource()
    {
        var source = Source(Chat("Ａ question", metadata: "first"), Chat("A   question", metadata: "second"), Chat("other"));
        var before = File.ReadAllBytes(source);
        var report = DatasetDeduplicator.Preview(source);
        Assert.Equal(3, report.SourceRows);
        Assert.Equal(3, report.ValidExamples);
        Assert.Equal(2, report.UniqueExamples);
        Assert.Equal(1, report.DuplicateExamples);
        Assert.Equal(new DuplicateOccurrence(2, 1), Assert.Single(report.Duplicates));
        Assert.True(report.CanExport);
        Assert.True(report.HasFindings);
        Assert.Equal(before, File.ReadAllBytes(source));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(before)).ToLowerInvariant(), report.SourceSha256);
    }

    [Fact]
    public void ExportRetainsFirstRawRecordsTheirOrderAndMetadataWithLineProvenance()
    {
        var first = Chat("one", metadata: "preserve-this");
        var third = Chat("two", metadata: "preserve-this-too");
        var source = Source(first, Chat("one", metadata: "later"), third, Chat("two", metadata: "later-too"));
        var original = File.ReadAllBytes(source);
        var output = PathOf("out.jsonl");
        var manifest = DatasetDeduplicator.Export(source, output, PathOf("manifest.json"));
        Assert.Equal(new[] { first, third }, File.ReadAllLines(output));
        Assert.Equal(new[] { 1, 3 }, manifest.KeptSourceLines);
        Assert.Equal(new[] { new DuplicateOccurrence(2, 1), new DuplicateOccurrence(4, 3) }, manifest.RemovedDuplicates);
        Assert.Equal(4, manifest.SourceRows);
        Assert.Equal(2, manifest.OutputRows);
        Assert.Equal(2, manifest.RemovedRows);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output))).ToLowerInvariant(), manifest.OutputSha256);
        Assert.Equal(original, File.ReadAllBytes(source));
        Assert.Empty(DatasetGuard.Check(output).Diagnostics);
    }

    [Fact]
    public void ConflictsAreDistinctFromDuplicatesAndNeverResolvedByExport()
    {
        var source = Source(Chat("same", "one"), Chat("same", "two"), Chat("same", "two"));
        var report = DatasetDeduplicator.Preview(source);
        Assert.Equal(1, report.ConflictingPromptGroups);
        Assert.Equal(1, report.DuplicateExamples);
        Assert.Equal(new DuplicateOccurrence(3, 2), Assert.Single(report.Duplicates));
        Assert.Equal("TSG004", Assert.Single(report.Diagnostics).Code);
        Assert.False(report.CanExport);
        Assert.Throws<InputException>(() => DatasetDeduplicator.Export(source, PathOf("out"), PathOf("manifest")));
        Assert.False(File.Exists(PathOf("out")));
        Assert.False(File.Exists(PathOf("manifest")));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("")]
    [InlineData("{\"messages\":[],\"messages\":[]}")]
    public void InvalidRecordsRemainVisibleAndPreventPartialExport(string invalid)
    {
        var source = Source(Chat("valid"), invalid, Chat("valid"));
        var report = DatasetDeduplicator.Preview(source);
        Assert.Equal(3, report.SourceRows);
        Assert.Equal(1, report.InvalidExamples);
        Assert.Equal(2, report.ValidExamples);
        Assert.Equal(1, report.DuplicateExamples);
        Assert.False(report.CanExport);
        Assert.Equal(2, Assert.Single(report.Diagnostics).Line);
        Assert.Throws<InputException>(() => DatasetDeduplicator.Export(source, PathOf("out"), PathOf("manifest")));
        Assert.False(File.Exists(PathOf("out")));
    }

    [Fact]
    public void EmptyFileCannotBeExported()
    {
        var report = DatasetDeduplicator.Preview(Source());
        Assert.Equal("TSG006", Assert.Single(report.Diagnostics).Code);
        Assert.False(report.CanExport);
    }

    [Fact]
    public void MultiTurnPersianAndSystemContextUseTheSharedFingerprintPolicy()
    {
        const string first = """{"messages":[{"role":"system","content":"کمک کن"},{"role":"user","content":"سلام"},{"role":"assistant","content":"درود"},{"role":"user","content":" سؤال "},{"role":"assistant","content":"پاسخ"}],"source":"first"}""";
        var second = first.Replace(" سؤال ", "سؤال").Replace("first", "second");
        var third = first.Replace("کمک کن", "پاسخ کوتاه بده");
        var source = Source(first, second, third);
        var report = DatasetDeduplicator.Preview(source);
        Assert.Equal(1, report.DuplicateExamples);
        Assert.Equal(0, report.ConflictingPromptGroups);
        DatasetDeduplicator.Export(source, PathOf("out"), PathOf("manifest"));
        Assert.Empty(DatasetGuard.Check(PathOf("out")).Diagnostics);
    }

    [Fact]
    public void CaseDifferencesArePreserved()
    {
        var report = DatasetDeduplicator.Preview(Source(Chat("Question"), Chat("question")));
        Assert.Equal(2, report.UniqueExamples);
        Assert.False(report.HasFindings);
    }

    [Fact]
    public void ExistingAndAliasedPathsAreRefusedWithoutModifyingAnything()
    {
        var source = Source(Chat("one"));
        var existing = PathOf("existing"); File.WriteAllText(existing, "preserve");
        Assert.Throws<InputException>(() => DatasetDeduplicator.Export(source, existing, PathOf("manifest")));
        Assert.Throws<InputException>(() => DatasetDeduplicator.Export(source, PathOf("new"), existing));
        Assert.Throws<InputException>(() => DatasetDeduplicator.Export(source, PathOf("same"), PathOf("same")));
        Assert.Throws<InputException>(() => DatasetDeduplicator.Export(source, source, PathOf("manifest")));
        Assert.Equal("preserve", File.ReadAllText(existing));
        Assert.False(File.Exists(PathOf("new")));
    }

    [Fact]
    public void DeterministicExportsHaveIdenticalBytesAndManifests()
    {
        var source = Source(Chat("one"), Chat("one"), Chat("two"));
        var first = DatasetDeduplicator.Export(source, PathOf("a"), PathOf("ma"));
        var second = DatasetDeduplicator.Export(source, PathOf("b"), PathOf("mb"));
        Assert.Equal(first.OutputSha256, second.OutputSha256);
        Assert.Equal(File.ReadAllBytes(PathOf("a")), File.ReadAllBytes(PathOf("b")));
        Assert.Equal(File.ReadAllBytes(PathOf("ma")), File.ReadAllBytes(PathOf("mb")));
    }

    [Fact]
    public void BomAndCrLfAreAcceptedWhileOutputIsUtf8WithoutBomAndLf()
    {
        var source = PathOf("source");
        var line = Chat("سلام", "پاسخ");
        File.WriteAllText(source, line + "\r\n" + line + "\r\n", new UTF8Encoding(true));
        DatasetDeduplicator.Export(source, PathOf("out"), PathOf("manifest"));
        Assert.Equal(Encoding.UTF8.GetBytes(line + "\n"), File.ReadAllBytes(PathOf("out")));
    }

    [Fact]
    public void CliPreviewAndExportUseExplicitCommandsAndValueFreeReports()
    {
        var source = Source(Chat("synthetic-private-prompt", "synthetic-private-answer"), Chat("synthetic-private-prompt", "synthetic-private-answer"));
        var output = new StringWriter();
        Assert.Equal(1, Application.Run(["dedup", "--input", source], output, new StringWriter()));
        using var preview = JsonDocument.Parse(output.ToString());
        Assert.True(preview.RootElement.GetProperty("canExport").GetBoolean());
        Assert.DoesNotContain("synthetic-private", output.ToString());
        Assert.Single(Directory.GetFiles(root));
        output = new StringWriter();
        Assert.Equal(0, Application.Run(["dedup-export", "--input", source, "--output", PathOf("out"), "--manifest", PathOf("manifest")], output, new StringWriter()));
        Assert.DoesNotContain("synthetic-private", output.ToString());
        Assert.Single(File.ReadAllLines(PathOf("out")));
    }

    [Fact]
    public void PreviewRejectsExportOptionsAndExportRequiresBothDestinations()
    {
        Assert.Equal(2, Application.Run(["dedup", "--input", "unused", "--output", "unused"], new StringWriter(), new StringWriter()));
        Assert.Equal(2, Application.Run(["dedup-export", "--input", "unused", "--output", "unused"], new StringWriter(), new StringWriter()));
    }

    public void Dispose()
    {
        if (Directory.Exists(root) && Path.GetFileName(root).StartsWith("trainset-dedup-tests-", StringComparison.Ordinal)) Directory.Delete(root, true);
    }
}
