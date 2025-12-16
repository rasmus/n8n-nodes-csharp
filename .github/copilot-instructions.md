# Copilot instructions

These instructions are for GitHub Copilot working in this repository.

## Project summary

- This repo provides an n8n community node that runs user-provided C# scripts.
- C# execution happens out-of-process via a .NET runner (`dotnet <runner.dll>`).
- The npm package is published to GitHub Packages and includes the compiled runner artifacts.
- GitHub Packages access is private (not public). Installation docs must reflect private registry authentication.

## Golden rules (do not violate)

1) Do not embed CLR in-process

- Do not propose or implement in-process CLR hosting (e.g., edge-style embedding).
- Keep execution as a spawned `dotnet` runner process.

2) Runner stdout must be JSON-only

- The runner must write exactly one JSON response to stdout.
- Any logs belong on stderr.

3) Releases publish npm only

- Only publish the npm package to GitHub Packages.
- Do not add Docker image publishing to the release pipeline.
- Release is gated:
  - branch must be `release`
  - tag `vX.Y.Z` must point at `release` HEAD
  - `X.Y.Z` must match `n8n-nodes-csharp/package.json`

4) Keep private registry constraints in mind

- Documentation and install flows must assume the GitHub Packages npm registry is private.
- Never hard-code or commit tokens.

## Where the truth lives

Start here before changing behavior:

- `docs/architecture.md` (runtime behavior + JSON contract)
- `docs/releasing.md` (release rules and steps)
- `docs/installation-self-hosted.md` (manual install + private registry)
- Root `README.md` (top-level usage)

## Implementation details to preserve

- Runner path override is via `N8N_CSHARP_RUNNER_PATH`.
- The node sends `{ mode, items, code }` to stdin and expects `{ ok, items | error }` from stdout.
- Return normalization: objects pass through, arrays expand, scalars wrap as `{ "value": ... }`.
- Avoid re-parenting `JsonNode` trees; deep-clone where necessary.

## Change discipline

- Keep changes minimal and aligned with existing patterns.
- If you change the runner contract or globals, update:
  - node implementation
  - runner tests
  - `docs/architecture.md`
