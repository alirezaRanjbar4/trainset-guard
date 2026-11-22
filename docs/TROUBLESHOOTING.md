# Troubleshooting

Commands target the **0.3.1** package and synthetic repository examples. Run from the repository root and use fresh output paths. See the [README](../README.md) for report fields/codes and the [design guide](DESIGN.md) for command boundaries and identity rules.

## Installation and executable discovery

Download `TrainSetGuard.Tool.0.3.1.nupkg` and `SHA256SUMS.txt` from the [release](https://github.com/alirezaRanjbar4/trainset-guard/releases/tag/v0.3.1) into an empty `downloaded-package` folder. Verify the package SHA-256, then install into a fresh isolated directory:

```sh
dotnet tool install TrainSetGuard.Tool --version 0.3.1 --tool-path ./artifacts/handoff-tool --add-source ./downloaded-package
```

Run `./artifacts/handoff-tool/trainset-guard --version` on Linux/macOS or `.\artifacts\handoff-tool\trainset-guard.exe --version` in PowerShell. Check `dotnet --info` if the .NET 10 runtime is unavailable. A package-source error can mean the download folder or version is wrong: this package is not published on NuGet.org. For an existing isolated installation, use `dotnet tool update` with the same package/version/source/tool-path options. Global installs use `--global` instead of `--tool-path ...`; inspect `dotnet tool list --global` and PATH if the command is missing.

The remaining examples use `trainset-guard` as shorthand for the intended executable.

## Exit `1` from preview can still allow export

```sh
trainset-guard dedup --input examples/duplicates.jsonl
```

The sample returns `1`: four valid rows contain two unique pairs and two duplicates. It reports `hasFindings: true` and `canExport: true`. Review the duplicate-to-kept line mappings; exit `1` is a finding signal, not automatic permission to discard records. Export is a separate explicit action:

```sh
trainset-guard dedup-export --input examples/duplicates.jsonl --output artifacts/handoff-run/unique.jsonl --manifest artifacts/handoff-run/dedup.json
trainset-guard check --train artifacts/handoff-run/unique.jsonl
```

Both commands return `0` for this sample. The export keeps source lines 1 and 3 with their original metadata. Reusing either destination is refused with exit `2`. Choose a new run directory rather than expecting an overwrite option.

## Conflicts, malformed rows, and empty files

`check`/`dedup` report dataset findings with exit `1`; invalid arguments or unreadable files return `2`. `split` and `dedup-export` refuse malformed/conflicting data with exit `2`, before writing datasets. They do not export the valid subset or choose between answers. An empty file is also refused; `check`/preview report TSG006 at line `0`.

For TSG001, inspect the reported physical line: it must be one JSON object, not a multiline object, array, blank line, or a record with duplicate properties. For TSG002, check role order, lowercase role names, nonblank string content, and a final assistant message. Provider-specific tool calls and multimodal/developer-role formats are outside the supported schema.

TSG004 means one normalized prompt has different final answers. Review those records and their provenance yourself. In preview, `uniqueExamples` counts candidate valid pairs, including distinct conflicting answers; it does not mean export is safe. `canExport: false` takes precedence. Message text is omitted from reports, so inspect the original source privately using the line numbers.

## Split fraction is a group fraction

```sh
trainset-guard split --input examples/train.jsonl --train-output artifacts/handoff-run/train.jsonl --validation-output artifacts/handoff-run/validation.jsonl --manifest artifacts/handoff-run/split.json --validation-fraction 0.34 --seed 42
trainset-guard check --train artifacts/handoff-run/train.jsonl --validation artifacts/handoff-run/validation.jsonl
```

The fraction must be strictly between `0` and `1`; at least two prompt groups are required. The sample's three groups produce one validation group. Larger/unequal duplicate groups can yield a different row fraction. Inspect requested/actual fractions and group/row counts in the manifest. Split keeps duplicates together, so a later `check` can still report TSG003 within a split even though no prompt crosses the split boundary.

A different system instruction or earlier turn changes prompt identity. These checks do not catch paraphrases, entity overlap, or other semantic leakage. Reusing seed and input reproduces current workflow outputs across fresh destination paths; changing the seed may change assignments.

## No-overwrite paths and write failures

Split requires three distinct fresh paths; export requires two. Existing files **or directories** at a destination are rejected. Quote paths containing spaces and check directory permissions. After an I/O failure, inspect all output paths: cleanup of files created by that invocation is best effort. Pre-existing files are preserved; parent directories may remain. JSON reports go to stdout, so direct shell redirection only to a fresh report path that is separate from inputs and dataset outputs.

## Encoding, line endings, and changing hashes

Use UTF-8 JSONL for interoperability. The readers recognize supported encoding BOMs; split/dedup use a strict UTF-8 fallback for input without a BOM. `check` is not a complete encoding validator. A decoding/read failure may be reported as a generic file error (`2`). Physical LF/CRLF records are read line by line; do not split one JSON object across lines.

Split/export emit UTF-8 without BOM and LF line endings with a final LF. They preserve retained JSON record text, not the original file's encoding or newline bytes. Source hashes identify exact original bytes; normalized fingerprints identify prompts/answers under a different policy. Equal fingerprints do not imply equal byte hashes. A preview and later export each read their own snapshot: compare their source hashes if another process can change the input.

## Large datasets and provider rejection

Split and both dedup commands hold a source snapshot and row/group structures in memory. Use inputs that fit available memory; there is no streaming-export flag. Checking independently chunked files does not establish duplicate/leakage freedom across chunks. The checker also retains hashes and findings, so it does not have constant memory use.

Passing these checks does not establish provider acceptance, token limits, privacy/PII safety or training quality. Check the target provider's current requirements separately. Never share private dataset outputs in a bug report; provide a small synthetic case, tool version, command, expected result and platform via [issues](https://github.com/alirezaRanjbar4/trainset-guard/issues/new/choose). See [CONTRIBUTING](../CONTRIBUTING.md).
