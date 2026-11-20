using Tooling;
namespace TrainSetGuard;

public static class Application
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args is ["--help"] or ["-h"])
        {
            output.WriteLine("trainset-guard check --train train.jsonl [--validation validation.jsonl]\nText-only chat JSONL checks. Exit: 0 clean, 1 data findings, 2 invalid arguments or file error. No raw message content is printed.");
            return 0;
        }
        if (args is ["--version"]) { output.WriteLine("0.1.0"); return 0; }
        try
        {
            if (args.Length == 0 || args[0] != "check") throw new InputException("Use the check command; see --help.");
            var options = new Options(args.Skip(1), "--train", "--validation");
            var report = DatasetGuard.Check(options.Require("--train"), options.Get("--validation"));
            output.WriteLine(JsonSupport.Serialize(report));
            return report.Errors > 0 ? 1 : 0;
        }
        catch (InputException exception) { error.WriteLine(exception.Message); return 2; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        { error.WriteLine("Input could not be read. Check file paths and text encoding."); return 2; }
    }
}
