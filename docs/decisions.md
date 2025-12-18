# Decisions

This document captures the “why” behind major choices, so future changes (including Copilot-assisted ones) don’t accidentally undo important constraints.

## Out-of-process execution via a runner process

Decision:

- Execute C# by spawning a separate `.NET` runner process.

Why:

- n8n runs on Node.js; in-process CLR embedding approaches are brittle across platforms and container environments.
- A separate runner is easier to debug, version, and sandbox.
- Crashes/GC issues stay isolated from the n8n process.

## Stdout is JSON-only

Decision:

- Runner writes exactly one JSON response to stdout.
- Script `Console.Out` is redirected to stderr.

Why:

- The n8n node expects to parse stdout as JSON.
- Mixing logs and JSON makes failures hard to diagnose and breaks parsing.

## Package includes the runner

Decision:

- The published npm package includes `runner/` artifacts.

Why:

- Keeps installation simple: installing the node package provides the runner without extra build steps.

Constraint:

- For published releases on Linux (x64/arm64, glibc/musl), the npm package ships self-contained runner executables, so `dotnet` is not required by default.
- If you override `N8N_CSHARP_RUNNER_PATH` to point at a `.dll`, then the runtime must have `dotnet` available.

## Release safety gates

Decision:

- Publish only from the `release` branch.
- Require a tag `vX.Y.Z` on `release` HEAD.
- `X.Y.Z` must match `n8n-nodes-csharp/package.json`.

Why:

- Prevent accidental publishes from the wrong branch/commit.
- Ensures the published version is explicitly intentional.

## Publish target

Decision:

- Publish only the npm package to npmjs.

Why:

- This repo’s distribution model is an n8n node package.
- Docker images are for local testing and are not part of the npm release output.
