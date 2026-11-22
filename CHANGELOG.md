# Changelog

## 0.3.1 — 2026-11-22

Added linked design and troubleshooting guides for shared fingerprints, validation, snapshots, exclusive writes, preview/export outcomes, grouped fractions, encoding and memory limits. Bundle those guides and linked root documents/license in the final tool package; update installation/version references for the first-scope closing release. Dataset processing and report/exit contracts are unchanged from 0.3.0. Release acceptance evidence is tracked in issue #4.

## 0.3.0 — 2026-11-21

Added read-only deduplication previews and explicit exports with first-occurrence order/metadata, duplicate line provenance, source/output hashes and no-overwrite destinations. Export refuses malformed conversations and conflicting final answers instead of resolving or dropping them automatically. Reports remain free of message text.

## 0.2.0 — 2026-11-21

Added deterministic prompt-group splitting, no-overwrite outputs, source/output hashes, and manifests; validates all rows and preserves duplicates without cross-split prompt leakage.

## 0.1.0 — 2026-11-20

Initial usable release with a C# CLI, synthetic examples, automated tests, and .NET tool packaging. See the README for the supported scope and limitations.
