# Roadmap

## Available in v0.1.0

- Text-only chat JSONL validation and stable, value-free diagnostics.
- Normalized exact duplicates, conflicting final answers, and prompt leakage across train/validation.
- Unicode-aware normalization and English/Persian synthetic examples.

## Delivered in v0.2.0

Added deterministic prompt-group splitting, no-overwrite outputs, source/output hashes, and manifests; validates all rows and preserves duplicates without cross-split prompt leakage.

## Next

- Optional system-message-independent leakage checks with explicit tradeoffs.
- Complete concise architecture and troubleshooting documentation for the bounded first stable scope.
- Optional near-duplicate detection with measured precision and recall on public fixtures.

Token limits, PII detection, tool-call schemas, provider-specific acceptance, and silent automatic cleaning are outside the current release. Plans depend on concrete feedback.

## Delivered in v0.3.0

- Read-only normalized exact duplicate/conflict previews and explicit first-occurrence exports.
- Counts, kept/removed source-line provenance, hashes and fresh output paths; invalid/conflicting input never exports partially.
