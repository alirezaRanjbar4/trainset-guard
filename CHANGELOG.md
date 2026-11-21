# Changelog

## 0.3.0 — 2026-11-21

Added read-only deduplication previews and explicit exports with first-occurrence order/metadata, duplicate line provenance, source/output hashes and no-overwrite destinations. Export refuses malformed conversations and conflicting final answers instead of resolving or dropping them automatically. Reports remain free of message text.

## 0.2.0 — 2026-11-21

Added deterministic prompt-group splitting, no-overwrite outputs, source/output hashes, and manifests; validates all rows and preserves duplicates without cross-split prompt leakage.

## 0.1.0 — 2026-11-20

Initial usable release with a C# CLI, synthetic examples, automated tests, and .NET tool packaging. See the README for the supported scope and limitations.
