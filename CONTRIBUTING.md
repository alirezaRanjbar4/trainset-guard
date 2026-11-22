# Contributing

Start with a small, reproducible issue and synthetic data. Bug reports, tests, documentation fixes, and focused improvements are welcome.

Read the [design guide](docs/DESIGN.md) before changing identity, snapshots or output writes. Keep the [troubleshooting guide](docs/TROUBLESHOOTING.md) and its current-release commands consistent with behavior.

- Keep changes tied to a concrete user problem.
- Add regression tests for changed behavior and CLI exit codes.
- Run `dotnet test -c Release` and `dotnet format --verify-no-changes`.
- Keep reports free of raw training text and reference/prediction contents.
- Include expected results and validation in pull requests.
- Discuss large changes before implementing them.

Use respectful, technical feedback. Never commit private datasets, credentials, generated model artifacts, or misleading benchmark claims.
