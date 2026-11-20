using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Tooling;

namespace TrainSetGuard;

public sealed record ChatMessage(string? Role, string? Content);
public sealed record ChatExample(List<ChatMessage?>? Messages);
public sealed record Finding(string Code, string File, int Line, int? RelatedLine, string Message);
public sealed record DatasetReport(int TrainRows, int ValidationRows, int ValidExamples, IReadOnlyList<Finding> Diagnostics)
{
    public int Errors => Diagnostics.Count;
}

public static partial class DatasetGuard
{
    private sealed record Seen(string AnswerHash, int Line);

    public static DatasetReport Check(string trainPath, string? validationPath = null)
    {
        var findings = new List<Finding>();
        var training = new Dictionary<string, Seen>(StringComparer.Ordinal);
        var trainRows = 0;
        var validationRows = 0;
        var valid = 0;
        Read(trainPath, training, null, ref trainRows);
        if (validationPath is not null)
            Read(validationPath, new(StringComparer.Ordinal), training, ref validationRows);
        return new(trainRows, validationRows, valid, findings);

        void Read(string path, Dictionary<string, Seen> seen, Dictionary<string, Seen>? previous, ref int rows)
        {
            foreach (var line in File.ReadLines(path))
            {
                rows++;
                ChatExample example;
                try { example = JsonSupport.Parse<ChatExample>(line); }
                catch (JsonException)
                {
                    findings.Add(new("TSG001", path, rows, null, "Invalid JSON object or duplicate JSON properties."));
                    continue;
                }
                var messages = example.Messages;
                if (!ValidConversation(messages))
                {
                    findings.Add(new("TSG002", path, rows, null, "Expected nonblank text messages: optional initial system, then alternating user/assistant pairs."));
                    continue;
                }
                valid++;
                var normalized = messages!.Select(m => new ChatMessage(m!.Role, Normalize(m.Content!))).ToArray();
                var promptHash = Hash(JsonSerializer.Serialize(normalized[..^1]));
                var answerHash = Hash(normalized[^1].Content!);
                if (seen.TryGetValue(promptHash, out var first))
                    findings.Add(new(first.AnswerHash == answerHash ? "TSG003" : "TSG004", path, rows, first.Line,
                        first.AnswerHash == answerHash ? "Duplicate normalized prompt and answer." : "Same normalized prompt has a conflicting final answer."));
                else
                    seen.Add(promptHash, new(answerHash, rows));
                if (previous is not null && previous.TryGetValue(promptHash, out var leaked))
                    findings.Add(new("TSG005", path, rows, leaked.Line, "Prompt also occurs in the training file; validation leakage."));
            }
            if (rows == 0)
                findings.Add(new("TSG006", path, 0, null, "Dataset is empty."));
        }
    }

    private static bool ValidConversation(List<ChatMessage?>? messages)
    {
        if (messages is null || messages.Count < 2 || messages.Any(m => m is null || string.IsNullOrWhiteSpace(m.Content)))
            return false;
        var start = messages[0]!.Role == "system" ? 1 : 0;
        if ((messages.Count - start) < 2 || (messages.Count - start) % 2 != 0)
            return false;
        for (var i = start; i < messages.Count; i++)
            if (messages[i]!.Role != ((i - start) % 2 == 0 ? "user" : "assistant"))
                return false;
        return true;
    }

    private static string Normalize(string value) => Whitespace().Replace(value.Normalize(NormalizationForm.FormKC).Trim(), " ");
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
