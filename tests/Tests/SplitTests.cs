using System.Security.Cryptography;
using System.Text.Json;
using Tooling;
using TrainSetGuard;

namespace Tests;

public sealed class SplitTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "trainset-split-tests-" + Guid.NewGuid().ToString("N"));
    private string PathOf(string name) { Directory.CreateDirectory(root); return Path.Combine(root, name); }
    private string Source(params string[] rows)
    {
        var path = PathOf(Guid.NewGuid().ToString("N") + ".jsonl");
        File.WriteAllLines(path, rows);
        return path;
    }
    private static string Chat(string prompt, string answer = "answer") => JsonSerializer.Serialize(new
    {
        messages = new[] { new { role = "user", content = prompt }, new { role = "assistant", content = answer } },
        metadata = "retained"
    });

    [Fact]
    public void KeepsDuplicateGroupsTogetherWithoutDroppingRowsAndRecordsHashes()
    {
        var source = Source(Chat("Ａ question"), Chat("A   question"), Chat("سلام دنیا"), Chat("other"));
        var train = PathOf("train"); var validation = PathOf("validation");
        var report = DatasetSplitter.Split(source, train, validation, PathOf("manifest"), 0.5, 123);
        Assert.Equal(4, report.SourceRows);
        Assert.Equal(3, report.PromptGroups);
        Assert.Equal(4, report.TrainRows + report.ValidationRows);
        Assert.Equal(2, report.ValidationGroups);
        var findings = DatasetGuard.Check(train, validation).Diagnostics;
        Assert.DoesNotContain(findings, row => row.Code is "TSG004" or "TSG005");
        Assert.Equal(1, findings.Count(row => row.Code == "TSG003"));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(train))).ToLowerInvariant(), report.TrainSha256);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant(), report.SourceSha256);
        Assert.All(File.ReadAllLines(train).Concat(File.ReadAllLines(validation)), line => Assert.Contains("metadata", line));
    }

    [Fact]
    public void SameSeedProducesIdenticalFilesAndManifestAcrossOutputPaths()
    {
        var source = Source(Chat("one"), Chat("two"), Chat("three"), Chat("four"));
        var first = DatasetSplitter.Split(source, PathOf("t1"), PathOf("v1"), PathOf("m1"), 0.25, 42);
        var second = DatasetSplitter.Split(source, PathOf("t2"), PathOf("v2"), PathOf("m2"), 0.25, 42);
        Assert.Equal(first, second);
        Assert.Equal(File.ReadAllBytes(PathOf("t1")), File.ReadAllBytes(PathOf("t2")));
        Assert.Equal(File.ReadAllBytes(PathOf("m1")), File.ReadAllBytes(PathOf("m2")));
    }

    [Fact]
    public void MultiTurnPersianRowsAreSupported()
    {
        const string multi = """{"messages":[{"role":"system","content":"کمک کن"},{"role":"user","content":"سلام"},{"role":"assistant","content":"درود"},{"role":"user","content":"سؤال"},{"role":"assistant","content":"پاسخ"}]}""";
        var report = DatasetSplitter.Split(Source(multi, multi, Chat("other")), PathOf("t"), PathOf("v"), PathOf("m"));
        Assert.Equal(3, report.SourceRows);
        Assert.Equal(2, report.PromptGroups);
    }

    [Fact]
    public void ConflictingAnswersAndMalformedInputWriteNothing()
    {
        foreach (var input in new[] { Source(Chat("same", "one"), Chat("same", "two")), Source(Chat("valid"), "{"), Source("{}") })
        {
            Assert.Throws<InputException>(() => DatasetSplitter.Split(input, PathOf("t"), PathOf("v"), PathOf("m")));
            Assert.False(File.Exists(PathOf("t")));
        }
    }

    [Fact]
    public void ExistingAndAliasedOutputPathsAreRejected()
    {
        var source = Source(Chat("one"), Chat("two"));
        var existing = PathOf("existing"); File.WriteAllText(existing, "preserve");
        Assert.Throws<InputException>(() => DatasetSplitter.Split(source, existing, PathOf("v"), PathOf("m")));
        Assert.Equal("preserve", File.ReadAllText(existing));
        Assert.Throws<InputException>(() => DatasetSplitter.Split(source, PathOf("same"), PathOf("same"), PathOf("m")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(double.NaN)]
    public void InvalidFractionsAreRejected(double value)
        => Assert.Throws<InputException>(() => DatasetSplitter.Split("unused", PathOf("t"), PathOf("v"), PathOf("m"), value));

    [Fact]
    public void SingleGroupCannotBeSplit() => Assert.Throws<InputException>(() => DatasetSplitter.Split(Source(Chat("one"), Chat("one")), PathOf("t"), PathOf("v"), PathOf("m")));

    public void Dispose()
    {
        if (Directory.Exists(root) && Path.GetFileName(root).StartsWith("trainset-split-tests-", StringComparison.Ordinal)) Directory.Delete(root, true);
    }
}
