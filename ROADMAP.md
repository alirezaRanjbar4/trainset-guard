# Roadmap

## Available in v0.1.0

- Text-only chat JSONL validation and stable, value-free diagnostics.
- Normalized exact duplicates, conflicting final answers, and prompt leakage across train/validation.
- Unicode-aware normalization and English/Persian synthetic examples.

## Next

- Optional system-message-independent leakage checks with explicit tradeoffs.
- Deterministic deduplication/splitting that keeps prompt groups together.
- Optional near-duplicate detection with measured precision and recall on public fixtures.

Token limits, PII detection, tool-call schemas, provider-specific acceptance, and automatic cleaning are outside the current release. Plans depend on concrete feedback.
