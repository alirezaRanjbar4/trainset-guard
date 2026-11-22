# Roadmap

## Available in v0.1.0

- Text-only chat JSONL validation and stable, value-free diagnostics.
- Normalized exact duplicates, conflicting final answers, and prompt leakage across train/validation.
- Unicode-aware normalization and English/Persian synthetic examples.

## Delivered in v0.2.0

Added deterministic prompt-group splitting, no-overwrite outputs, source/output hashes, and manifests; validates all rows and preserves duplicates without cross-split prompt leakage.

## Documentation handoff in v0.3.1

The [design guide](docs/DESIGN.md) and [troubleshooting guide](docs/TROUBLESHOOTING.md) cover the delivered workflows and commands for v0.3.1. Both guides, the contribution guide, roadmap, changelog and license are bundled with the README in the tool package, so its relative documentation links remain usable after packing.

## First-scope completion

The bounded scope includes text-chat validation, normalized duplicate/conflict/leakage reports, deterministic prompt-group splits, explicit dedup exports with provenance, and user/developer guides. Version 0.3.1 is the closing release for this scope. Completion evidence is recorded in issue #4 after package installation/examples, supported-platform CI, final-commit metadata and published tag/checksums pass. Keep the repository public and maintained for reproducible bugs and feedback after that handoff; optional future features do not reopen the completed scope.

## Future candidates

- Optional system-message-independent leakage checks with explicit tradeoffs.
- Optional near-duplicate detection with measured precision and recall on public fixtures.

Token limits, PII detection, tool-call schemas, provider-specific acceptance, and silent automatic cleaning are outside the current release. Plans depend on concrete feedback.

## Delivered in v0.3.0

- Read-only normalized exact duplicate/conflict previews and explicit first-occurrence exports.
- Counts, kept/removed source-line provenance, hashes and fresh output paths; invalid/conflicting input never exports partially.
