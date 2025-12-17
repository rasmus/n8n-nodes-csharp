# Architecture

## High-level

This repo provides an n8n community node that runs user-provided C# scripts.

- Node package: `n8n-nodes-csharp/` (TypeScript)
- Runner: `runner/N8n.CSharpRunner/` (.NET 8 console app)

The node **does not** execute C# in-process. It spawns a separate `dotnet` process and exchanges data using JSON over stdin/stdout.

The runner is shipped as **self-contained executables** for common Linux environments (glibc + musl, x64 + arm64). The node detects the runtime and spawns the matching executable directly.

## Components

### n8n node (TypeScript)

- Implements the n8n `INodeType` execute method.
- Collects input items and sends them to the runner as plain JSON.
- Parses the runner response and returns output items to n8n.

Runner path resolution:

- If `N8N_CSHARP_RUNNER_PATH` is set, the node uses it.
- Otherwise it uses the runner shipped inside the npm package:
  - On Linux x64/arm64: prefers `runner/<rid>/N8n.CSharpRunner` (auto-detected between `linux-*` and `linux-musl-*`)
  - Fallback: `runner/N8n.CSharpRunner` / `runner/N8n.CSharpRunner.dll`

### .NET runner (C#)

- Reads a JSON request from stdin.
- Compiles and executes the provided C# using Roslyn scripting.
- Writes exactly one JSON response to stdout.

The runner redirects `Console.Out` to stderr while executing user code to keep stdout reserved for the JSON response.

## Data contract

### Request (stdin)

Sent from the node to the runner:

```json
{
  "mode": "allItems" | "perItem",
  "items": [ {"...": "..."}, {"...": "..."} ],
  "code": "// C# script"
}
```

### Response (stdout)

Runner returns either success:

```json
{ "ok": true, "items": [ {"json": "..."}, {"json": "..."} ] }
```

Or error:

```json
{ "ok": false, "error": { "message": "...", "detail": "..." } }
```

Notes:

- Stdout must be valid JSON. Any user logging should go to stderr.
- n8n converts the returned `items` into output items.

## Execution modes

### All Items

- Script runs once.
- Globals:
  - `Items` (`JsonArray?`) contains all input items.
  - `Item` and `Index` are null.

### Per Item

- Script runs once per input item.
- Globals:
  - `Items` is the full array.
  - `Item` is the current item.
  - `Index` is the current index.

## Return-value normalization

The runner normalizes return values into “n8n items”:

- Object → one output item
- Array → multiple output items
- Scalar / non-object → wrapped into `{ "value": ... }`

Important implementation detail:

- `JsonNode` instances cannot be attached to multiple parents.
- When the script returns an array, elements already have a parent (`JsonArray`), so the runner deep-clones elements before returning/wrapping them.

## Convenience helpers

The runner adds extension helpers for `System.Text.Json.Nodes.JsonNode`:

- `node.Str("field")` → `string?`
- `node.Int("field")` → `int?`
- `node.Bool("field")` → `bool?`

These are added via Roslyn imports so user scripts can be concise.
