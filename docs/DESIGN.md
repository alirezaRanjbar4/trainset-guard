# Design and completion boundary

TrainSet Guard prepares local text-only chat JSONL for review. Its bounded first scope is conversation validation, normalized exact duplicates/conflicts, cross-file prompt leakage, deterministic prompt-group splits, and reviewable first-occurrence deduplication exports. It does not train or call a model.

## Command boundaries

`Application.Run` parses options, dispatches commands, serializes JSON reports to stdout, and selects exit codes. Argument/file/operation errors go to stderr with exit `2`. `DatasetGuard` owns the shared conversation validator and fingerprint policy. `DatasetSplitter` and `DatasetDeduplicator` reuse that policy for their own processing and output contracts.

| Command | Processing | Successful/report outcome |
| --- | --- | --- |
| `check` | Reads lines incrementally; retains prompt/answer hashes, first-line references, and findings | `0` clean, `1` findings |
| `split` | Reads one byte snapshot, validates every row, then assigns whole prompt groups | `0` after writing two datasets and a manifest |
| `dedup` | Inspects one byte snapshot; produces counts, duplicate mappings and diagnostics | `0` clean, `1` findings, including exportable duplicates |
| `dedup-export` | Inspects its own current snapshot and keeps first occurrences only if the entire input is valid/conflict-free | `0` after writing a dataset and manifest |

`JsonSupport` rejects duplicate property names recursively before deserializing. Each physical line must be one complete JSON record. The supported conversation has an optional initial `system`, then alternating lowercase `user`/`assistant` roles ending in `assistant`; all content is a nonblank string. Extra properties are retained as metadata when datasets are written, but are not a provider-specific schema validation.

## Shared identity: `chat-nfkc-v1`

Normalize message content with Unicode NFKC, trimming and collapsed whitespace; preserve case. The prompt digest is SHA-256 over the serialized normalized message sequence excluding only the final assistant answer. Roles, initial system instructions and prior turns remain part of prompt identity. The normalized final answer has a separate SHA-256 digest. Extra metadata does not affect either identity.

`check` compares each prompt against its first answer within a file and against the supplied training prompts for leakage. Deduplication also maps each distinct `(prompt, answer)` pair to its first physical line. Thus an exact repetition of a conflicting answer can count as a duplicate while the whole prompt group remains unexportable. Different answers are flagged for review; no rule automatically selects the correct answer.

The policy detects normalized exact identities, not paraphrases or shared-entity leakage. Different system instructions produce different prompts. Fingerprints are identifiers for this policy, not proof that a dataset is independent or free of private information.

## Snapshot processing and deterministic output

Split/dedup read the source bytes once per invocation, compute the source hash from those bytes, and parse that snapshot. A preview does not lock in a later export: export re-inspects its own snapshot. Compare hashes if the input may have changed between commands.

Split rejects invalid rows and conflicting answers before writing. It ranks prompt groups by SHA-256 of invariant seed text plus the prompt digest, with prompt-digest tie breaking. The requested fraction selects groups, rounded away from zero at the midpoint and clamped to keep both sides nonempty. All rows of a group stay together, including exact duplicates. Row fractions can therefore differ from the requested group fraction.

Dedup export refuses any invalid row, conflict, or empty input. When permitted, it preserves the first original JSON line for each normalized pair, including that line's metadata, and retains source order. Neither workflow rewrites message text to its normalized form. Output datasets use UTF-8 without BOM, LF separators, and a final LF; source-byte hashes and output-byte hashes can differ because of encoding/line endings as well as row selection.

## Exclusive writes and report privacy

Output destinations must be distinct fresh paths. Parent directories are created as needed; files use `FileMode.CreateNew`. A failed write triggers best-effort cleanup of files created by that invocation, never deletion of pre-existing files. This is not a cross-file atomic filesystem transaction; inspect destinations after an I/O failure. Successful invocations preserve the source file.

Reports/manifests expose hashes, counts, paths, physical line numbers, duplicate-to-kept mappings and fixed diagnostic messages, without message contents. Explicit dataset outputs contain original records and metadata. Hashes do not anonymize those outputs; keep private source-derived files private.

`check` retains comparison state rather than full rows. Split/dedup retain the input bytes and parsed/retained row data in memory, plus grouping/report structures. There is no streaming export or configurable memory quota in this scope.

## Verification and handoff

Tests cover validation, normalization, conflicts/leakage, group fractions, deterministic output, preserved metadata/provenance, and no-overwrite behavior. CI tests, verifies formatting and packages the CLI on Windows, Linux and macOS. See [CONTRIBUTING](../CONTRIBUTING.md), [troubleshooting](TROUBLESHOOTING.md) and the [README](../README.md).

The user/developer guides complete the documentation portion of the bounded scope. Issue #4 still requires a validated closing release before maintenance status. Near-duplicate detection, PII scanning, token/provider limits and extra role/tool-call schemas remain optional future work; see the [roadmap](../ROADMAP.md).
