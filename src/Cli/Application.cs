using Tooling;
namespace TrainSetGuard;

public static class Application
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args is ["--help"] or ["-h"])
        {
            output.WriteLine("trainset-guard check --train train.jsonl [--validation validation.jsonl]\ntrainset-guard split --input data.jsonl --train-output train.jsonl --validation-output validation.jsonl --manifest split.json [--validation-fraction 0.2] [--seed 42]\nText-only chat JSONL checks and deterministic prompt-group splitting. Exit: 0 success, 1 check findings, 2 invalid arguments/data or file error. Reports omit message content; split files preserve the supplied records.");
            return 0;
        }
        if (args is ["--version"]) { output.WriteLine("0.2.0"); return 0; }
        try
        {
            if (args.Length > 0 && args[0] == "split")
            {
                var splitOptions = new Options(args.Skip(1), "--input", "--train-output", "--validation-output", "--manifest", "--validation-fraction", "--seed");
                var split = DatasetSplitter.Split(splitOptions.Require("--input"), splitOptions.Require("--train-output"), splitOptions.Require("--validation-output"),
                    splitOptions.Require("--manifest"), splitOptions.Fraction("--validation-fraction", 0.2), splitOptions.Int("--seed", 42));
                output.WriteLine(JsonSupport.Serialize(split));
                return 0;
            }
            if (args.Length == 0 || args[0] != "check") throw new InputException("Use check or split; see --help.");
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
