# n8n-nodes-csharp

Community node package for n8n providing a **C# Code** node.

Published to GitHub Packages as `@rasmus/n8n-nodes-csharp`.

## Install in self-hosted n8n (manual install)

Follow n8n’s official manual install guide:

```text
https://docs.n8n.io/integrations/community-nodes/installation/manual-install/
```

Important for this package:

- The repository + GitHub Packages registry are private (not public).
- You must authenticate npm to `npm.pkg.github.com` to install this package.

Example (Docker):

```bash
docker exec -it n8n sh

mkdir -p ~/.n8n/nodes
cd ~/.n8n/nodes

npm config set @rasmus:registry https://npm.pkg.github.com
npm config set //npm.pkg.github.com/:_authToken "<YOUR_GITHUB_TOKEN>"

npm i @rasmus/n8n-nodes-csharp
```

Then restart n8n.

Note: if you run a framework-dependent runner (`.dll`), the C# node requires `dotnet` available at runtime. If you use a self-contained runner executable, `dotnet` is not required.

## Quick install (generic npm)

Install (requires npm configured for GitHub Packages):

```bash
npm config set @rasmus:registry https://npm.pkg.github.com
npm i @rasmus/n8n-nodes-csharp
```

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
