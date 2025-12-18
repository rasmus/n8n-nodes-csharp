# Self-hosted installation

The recommended approach is installing via the n8n UI (Community nodes), using the public npm registry.

Module name:

```text
@madoere/n8n-nodes-csharp
```

n8n docs (Community nodes):

https://docs.n8n.io/integrations/community-nodes/

This page keeps the manual install steps for environments where UI-based install isn’t available.

## Prerequisites

### `dotnet` available at runtime (sometimes)

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

3) Install the node package:

```bash
npm i @madoere/n8n-nodes-csharp
```

4) Restart n8n (example for docker compose):

```bash
exit
docker compose restart
```

## Upgrade

```bash
docker exec -it n8n sh
cd ~/.n8n/nodes
npm update @madoere/n8n-nodes-csharp
```

## Uninstall

```bash
docker exec -it n8n sh
cd ~/.n8n/nodes
npm uninstall @madoere/n8n-nodes-csharp
```

## Runner path override (optional)

By default, the node uses the runner shipped inside the npm package.

To override (for example, to point at a custom runner build), set:

- `N8N_CSHARP_RUNNER_PATH=/absolute/path/to/N8n.CSharpRunner` (executable)
- `N8N_CSHARP_RUNNER_PATH=/absolute/path/to/N8n.CSharpRunner.dll` (requires `dotnet`)
