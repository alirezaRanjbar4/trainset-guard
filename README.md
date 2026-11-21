# TrainSet Guard

[![CI](https://github.com/alirezaRanjbar4/trainset-guard/actions/workflows/ci.yml/badge.svg)](https://github.com/alirezaRanjbar4/trainset-guard/actions/workflows/ci.yml)
[![MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**Check chat training data before spending time or money on fine-tuning.**

A C# / .NET 10 CLI for malformed examples, duplicate prompts, conflicting answers, and prompt leakage between training and validation JSONL. Files stay local; reports include paths and line numbers without message contents.

## Quick start

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) on Windows, Linux, or macOS.

```sh
git clone https://github.com/alirezaRanjbar4/trainset-guard.git
cd trainset-guard
dotnet run --project src/Cli -- check --train examples/train.jsonl --validation examples/validation.jsonl
```

This passes with five synthetic examples. To see a leakage finding (exit code `1`):

```sh
dotnet run --project src/Cli -- check --train examples/train.jsonl --validation examples/leaky-validation.jsonl
```

## Input

One complete JSON object per line:

```json
{"messages":[{"role":"user","content":"How do I reset my password?"},{"role":"assistant","content":"Use the password reset link."}]}
```

The supported format is **text-only chat**: an optional initial `system` message, followed by alternating `user` and `assistant` messages, ending in `assistant`. Content must be a nonblank string. Multiple turns and extra metadata properties are allowed. Duplicate JSON property names are rejected.

Tool calls, multimodal content, developer roles, and provider-specific limits are outside v0.2.0. Passing these checks does not guarantee that a provider will accept the data. For example, OpenAI documents additional [fine-tuning format requirements](https://developers.openai.com/api/docs/guides/supervised-fine-tuning).

## What gets checked

| Code | Finding |
| --- | --- |
| TSG001 | Invalid JSON record, wrong JSON shape, or duplicate JSON properties |
| TSG002 | Unsupported or invalid chat sequence/content |
| TSG003 | Same normalized prompt and final answer repeated within one split |
| TSG004 | Same normalized prompt with a different final answer within one split |
| TSG005 | Validation prompt also appears in training, regardless of final answer |
| TSG006 | Empty dataset |

Normalization uses Unicode NFKC, trims text, and collapses whitespace. **Case is preserved.** Prompt identity includes message roles, the optional system instruction, and prior turns; only the final assistant answer is excluded. Digests use SHA-256 over an unambiguous serialized message sequence.

The checker reads one line at a time and retains hashes/line numbers for comparison. It detects normalized exact overlap, not semantic/near duplicates. Different system messages produce different prompt identities. A repeated prompt with different valid answers is flagged for review; it is not automatically removed.

## Install the tool

Download `TrainSetGuard.Tool.0.2.0.nupkg` from the [release](https://github.com/alirezaRanjbar4/trainset-guard/releases/tag/v0.2.0) into an empty folder:

```sh
dotnet tool install --global TrainSetGuard.Tool --version 0.2.0 --add-source ./downloaded-package
trainset-guard check --train train.jsonl --validation validation.jsonl
```

Or package from source:

```sh
dotnet pack src/Cli -c Release -o artifacts
dotnet tool install --global TrainSetGuard.Tool --version 0.2.0 --add-source ./artifacts
```

Distribution is through GitHub Releases; the package is not currently on NuGet.org. The tool has no runtime package dependencies.

## Reports and CI

JSON goes to standard output. Diagnostics contain `code`, `file`, 1-based `line`, optional `relatedLine`, and a message. For TSG005, `relatedLine` points into the supplied training file; otherwise it points to the first matching row in the same file. Line `0` denotes an empty file.

```sh
trainset-guard check --train train.jsonl --validation validation.jsonl > dataset-report.json
```

Exit codes: `0` clean, `1` dataset findings, `2` invalid arguments or an unreadable input. Blank lines count as invalid JSON records. The check command never changes inputs. Use the explicit split command to create new datasets.

## Reproducible prompt-group splitting

```sh
trainset-guard split --input examples/train.jsonl --train-output artifacts/train.jsonl --validation-output artifacts/validation.jsonl --manifest artifacts/split.json --validation-fraction 0.34 --seed 42
trainset-guard check --train artifacts/train.jsonl --validation artifacts/validation.jsonl
```

`split` validates the entire input before writing, rejects malformed conversations and conflicting final answers for a normalized prompt, and preserves every original JSON record and extra metadata. Exact duplicates are retained together, so `check` may still flag duplicate rows inside a resulting split; no automatic deduplication is performed.

Groups use the same case-sensitive prompt fingerprint as `check`, including system instructions and prior turns. Group ranking is SHA-256 of invariant seed text and the prompt digest; ties sort by prompt digest. The same input and seed produce identical files across output paths. Each output preserves original input row order, encoded as UTF-8 without BOM with LF line endings.

The requested fraction selects **prompt groups**, rounded to nearest with midpoint away from zero and clamped to keep both splits nonempty. It must be strictly between 0 and 1, and at least two groups are required. Unequal group sizes can give a different row fraction; the manifest reports requested and actual fractions, row/group counts, seed, fingerprint policy, and source/output SHA-256 hashes. This is not stratified or semantic splitting.

All three destinations must be distinct fresh paths. Existing outputs are never overwritten; files are created exclusively, and files created by a failed write are removed where possible. The JSON manifest and console report omit message contents. The two split datasets intentionally contain the supplied records: keep them private when the source is private.

## Development

```sh
dotnet test -c Release
dotnet format --verify-no-changes
```

Use synthetic or redacted examples in [issues](https://github.com/alirezaRanjbar4/trainset-guard/issues/new/choose). See [contributing](CONTRIBUTING.md) and the [roadmap](ROADMAP.md).

## فارسی

این ابزار پیش از آموزش یا Fine-tuning، داده‌های گفت‌وگویی JSONL را بررسی می‌کند: قالب خراب، نمونه تکراری، پاسخ‌های ناسازگار و نشت سؤال‌های آموزش به اعتبارسنجی. متن داده در گزارش چاپ نمی‌شود. تشخیص فعلی بر پایه برابری پس از نرمال‌سازی است و تشابه معنایی یا اطلاعات شخصی را تشخیص نمی‌دهد.

[MIT License](LICENSE)
