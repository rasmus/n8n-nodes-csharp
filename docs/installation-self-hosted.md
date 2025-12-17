# Self-hosted installation (manual install)

This project is meant for self-hosted n8n.

The recommended approach is **manual install** (inside the n8n container), following the official n8n guide:

https://docs.n8n.io/integrations/community-nodes/installation/manual-install/

## Important: private GitHub Packages registry

This repository (and the GitHub Packages npm registry for it) is private.

That means:

- You must authenticate npm to `npm.pkg.github.com`.
- UI-based install flows that assume the public npm registry may not work.

## Prerequisites

### 1) A GitHub token that can read packages

Typically you’ll use a GitHub Personal Access Token (PAT) with:

- `read:packages`

Depending on your org/repo settings, you may also need repository read access.

Do not commit this token.

### 2) `dotnet` available at runtime

The node spawns the runner either as:

- `dotnet <runner.dll>` (framework-dependent), or
- `<runner executable>` (self-contained publish)

The published npm package includes self-contained runner executables for Linux (glibc + musl, x64 + arm64). In most Docker-based self-hosted setups, no extra `dotnet` runtime install is needed.

- If you use this repository’s Docker image setup, the runner is self-contained and `dotnet` is not required.
- If you run the stock `n8nio/n8n` image, you must either add the .NET runtime or override the runner path to a self-contained runner executable.

## Install (Docker)

1) Open a shell in the running n8n container:

```bash
docker exec -it n8n sh
```

2) Create the community nodes directory and enter it:

```bash
mkdir -p ~/.n8n/nodes
cd ~/.n8n/nodes
```

3) Configure the registry for the `@rasmus` scope:

```bash
npm config set @rasmus:registry https://npm.pkg.github.com
```

4) Authenticate npm to GitHub Packages (writes to `~/.npmrc` in the container):

```bash
npm config set //npm.pkg.github.com/:_authToken "<YOUR_GITHUB_TOKEN>"
```

5) Install the node package:

```bash
npm i @rasmus/n8n-nodes-csharp
```

6) Restart n8n (example for docker compose):

```bash
exit
docker compose restart
```

## Upgrade

```bash
docker exec -it n8n sh
cd ~/.n8n/nodes
npm update @rasmus/n8n-nodes-csharp
```

## Uninstall

```bash
docker exec -it n8n sh
cd ~/.n8n/nodes
npm uninstall @rasmus/n8n-nodes-csharp
```

## Runner path override (optional)

By default, the node uses the runner shipped inside the npm package.

To override (for example, to point at a custom runner build), set:

- `N8N_CSHARP_RUNNER_PATH=/absolute/path/to/N8n.CSharpRunner` (executable)
- `N8N_CSHARP_RUNNER_PATH=/absolute/path/to/N8n.CSharpRunner.dll` (requires `dotnet`)
