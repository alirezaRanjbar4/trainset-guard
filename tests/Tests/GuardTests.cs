using System.Text.Json;
using TrainSetGuard;
namespace Tests;

public sealed class GuardTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "trainset-guard-tests-" + Guid.NewGuid().ToString("N"));
    private string FileOf(params string[] lines)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".jsonl");
        File.WriteAllLines(path, lines);
        return path;
    }
    private static string Chat(string prompt, string answer) => JsonSerializer.Serialize(new { messages = new[] { new { role = "user", content = prompt }, new { role = "assistant", content = answer } } });

    [Fact]
    public void DistinctDataPasses()
    {
        var report = DatasetGuard.Check(FileOf(Chat("one", "a")), FileOf(Chat("two", "b")));
        Assert.Empty(report.Diagnostics);
        Assert.Equal(2, report.ValidExamples);
    }
    [Fact]
    public void DetectsWhitespaceAndUnicodeNormalizedDuplicates()
    {
        var report = DatasetGuard.Check(FileOf(Chat("Ａ  question", "answer"), Chat("A question", "answer")));
        var finding = Assert.Single(report.Diagnostics);
        Assert.Equal("TSG003", finding.Code);
        Assert.Equal(1, finding.RelatedLine);
        Assert.Equal(2, finding.Line);
    }
    [Fact]
    public void ConflictingAnswersAreSeparateFromExactDuplicates()
    {
        var report = DatasetGuard.Check(FileOf(Chat("question", "one"), Chat("question", "two")));
        Assert.Equal("TSG004", Assert.Single(report.Diagnostics).Code);
    }
    [Fact]
    public void DetectsLeakageEvenWhenAnswersDiffer()
    {
        var report = DatasetGuard.Check(FileOf(Chat("question", "one")), FileOf(Chat("question", "two")));
        Assert.Equal("TSG005", Assert.Single(report.Diagnostics).Code);
    }
    [Fact]
    public void DuplicateValidationPromptsAreDetected()
    {
        var report = DatasetGuard.Check(FileOf(Chat("other", "one")), FileOf(Chat("same", "two"), Chat("same", "two")));
        Assert.Equal("TSG003", Assert.Single(report.Diagnostics).Code);
    }
    [Theory]
    [InlineData("{", "TSG001")]
    [InlineData("", "TSG001")]
    [InlineData("[]", "TSG001")]
    [InlineData("null", "TSG001")]
    [InlineData("{\"messages\":[],\"messages\":[]}", "TSG001")]
    [InlineData("{}", "TSG002")]
    [InlineData("{\"messages\":[null,null]}", "TSG002")]
    [InlineData("{\"messages\":[{\"role\":\"user\",\"content\":\"hello\"}]}", "TSG002")]
    [InlineData("{\"messages\":[{\"role\":\"user\",\"content\":\"hello\"},{\"role\":\"assistant\",\"content\":\" \"}]}", "TSG002")]
    public void InvalidExamplesHaveStableSafeDiagnostics(string json, string code)
    {
        Assert.Equal(code, Assert.Single(DatasetGuard.Check(FileOf(json)).Diagnostics).Code);
    }
    [Fact]
    public void EmptyFileIsAnError() => Assert.Equal("TSG006", Assert.Single(DatasetGuard.Check(FileOf()).Diagnostics).Code);
    [Fact]
    public void MultiturnConversationWithSystemMessagePasses()
    {
        const string json = """{"messages":[{"role":"system","content":"help"},{"role":"user","content":"one"},{"role":"assistant","content":"two"},{"role":"user","content":"three"},{"role":"assistant","content":"four"}]}""";
        Assert.Empty(DatasetGuard.Check(FileOf(json)).Diagnostics);
    }
    [Fact]
    public void ReportsDoNotExposeMessages()
    {
        var file = FileOf(Chat("synthetic-private-prompt", "synthetic-private-answer"), Chat("synthetic-private-prompt", "synthetic-private-answer"));
        var output = new StringWriter();
        Assert.Equal(1, Application.Run(["check", "--train", file], output, new StringWriter()));
        using var parsed = JsonDocument.Parse(output.ToString());
        Assert.Equal(1, parsed.RootElement.GetProperty("errors").GetInt32());
        Assert.DoesNotContain("synthetic-private", output.ToString());
    }
    [Fact]
    public void InvalidArgumentsAndMissingFilesFailClearly()
    {
        Assert.Equal(2, Application.Run(["check"], new StringWriter(), new StringWriter()));
        Assert.Equal(2, Application.Run(["check", "--train", Path.Combine(root, "absent")], new StringWriter(), new StringWriter()));
    }
    public void Dispose()
    {
        if (Directory.Exists(root) && Path.GetFileName(root).StartsWith("trainset-guard-tests-", StringComparison.Ordinal)) Directory.Delete(root, true);
    }
}
