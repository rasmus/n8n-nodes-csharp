# n8n-nodes-csharp

Community node package for n8n providing a **C# Code** node.

## Security / sandboxing

This node executes **user-provided C# code with no sandboxing**.

Treat access to this node as equivalent to granting arbitrary code execution on the machine/container running n8n:

- The script can read/write files accessible to the n8n process, call the network, and consume CPU/memory.
- There are no built-in isolation guarantees, permissions, or resource limits beyond what your OS/container provides.

Only use this node in trusted environments (trusted workflows + trusted users), and rely on external isolation/hardening (separate instance, container/VM boundaries, least-privilege filesystem/networking, etc.).

Published to GitHub Packages as `@rasmus/n8n-nodes-csharp` and `@madoere/n8n-nodes-csharp` on the official [NPM registry](https://www.npmjs.com/package/@madoere/n8n-nodes-csharp).

## Install in self-hosted n8n (manual install)

Follow n8n’s official GUI or manual install guide:

```text
https://docs.n8n.io/integrations/community-nodes/installation/
```

## Supported platforms

This package bundles self-contained runner executables for:

- Linux (glibc): `linux-x64`, `linux-arm64`
- Linux (musl/Alpine): `linux-musl-x64`, `linux-musl-arm64`

On Linux x64/arm64, the node auto-detects libc (glibc vs musl) and selects the matching runner under `runner/<rid>/`.

On other platforms, set `N8N_CSHARP_RUNNER_PATH` to a custom runner (executable or `.dll`).

## How it works

- Node UI + integration: TypeScript (`nodes/CSharpCode/CSharpCode.node.ts`)
- C# execution: runner process (`runner/N8n.CSharpRunner` or `runner/N8n.CSharpRunner.dll`)

The node sends input items + your script via stdin JSON and reads output items from stdout JSON.

## Execution modes

- **All Items**: script runs once; use `Items`
- **Per Item**: script runs once per item; use `Item` + `Index`

## Globals

- `Items` (`System.Text.Json.Nodes.JsonArray?`) — input items
- `Item` (`System.Text.Json.Nodes.JsonNode?`) — current item (per-item mode)
- `Index` (`int?`) — current index (per-item mode)

## Convenience helpers

To avoid verbose JSON node access like `Item?["name"]?.GetValue<string>()`, the runner exposes a few helpers:

- `node.Str("name")` → `string?`
- `node.Int("count")` → `int?`
- `node.Bool("enabled")` → `bool?`

## Return value

Return either:

- a single object → one output item
- an array of objects → multiple output items

Non-object values are wrapped as `{ "value": ... }`.

## Examples

All Items (transform every input item):

```csharp

// Access fields from input items:
return Items!.Select((node, idx) => new {
	index = idx,
	name = node.Str("name"),
	message = node.Str("name") + " from C#"
}).ToArray();
```

Per Item (emit multiple output items per input item):

```csharp
return new object[] {
	new { index = Index, kind = "a", name = Item.Str("name") },
	new { index = Index, kind = "b", name = Item.Str("name") },
};
```

## Runner path

Default location (relative to this package):

- On Linux x64/arm64, the node auto-selects:
	- `runner/linux-x64/N8n.CSharpRunner`
	- `runner/linux-arm64/N8n.CSharpRunner`
	- `runner/linux-musl-x64/N8n.CSharpRunner`
	- `runner/linux-musl-arm64/N8n.CSharpRunner`

Fallback (legacy layout):

- `runner/N8n.CSharpRunner` or `runner/N8n.CSharpRunner.dll`

Override via env var:

- `N8N_CSHARP_RUNNER_PATH=/path/to/N8n.CSharpRunner` (executable) or `/path/to/N8n.CSharpRunner.dll`
