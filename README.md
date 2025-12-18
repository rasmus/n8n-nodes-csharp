# n8n-nodes-csharp — C# Code node for n8n

Run custom C# scripts inside n8n workflows, similar to n8n’s built-in Code nodes.

Package: https://www.npmjs.com/package/@madoere/n8n-nodes-csharp

## Install (recommended: n8n UI)

In n8n:

1) Go to **Settings** → **Community nodes**
2) Choose **Install** and enter:

```text
@madoere/n8n-nodes-csharp
```

3) Restart n8n if prompted

If your n8n deployment disables community nodes, enable them using n8n’s docs:

https://docs.n8n.io/integrations/community-nodes/

## Using the C# Code node

The node supports two execution modes:

- **All Items**: run once; use `Items`
- **Per Item**: run once per input item; use `Item` + `Index`

Return shapes:

- return an object → one output item
- return an array of objects → multiple output items
- return a scalar → wrapped as `{ "value": ... }`

Logging:

- The runner’s stdout is reserved for the JSON response.
- Write logs with `Console.Error.WriteLine(...)`.

Convenience helpers are available on JSON nodes:

- `node.Str("field")` → `string?`
- `node.Int("field")` → `int?`
- `node.Bool("field")` → `bool?`

Example: read `name` from each incoming item and emit a message:

```csharp
return Items!.Select(i => new {
    message = i.Str("name") + " from C#",
}).ToArray();
```

Example: per-item mode (one output per input item):

```csharp
return new {
    index = Index,
    message = Item.Str("name") + " from C#",
};
```

### Custom `// using ...` directives (including `System.Text`)

You can add namespace imports by placing special header comments at the top of your script:

```csharp
// using System.Text
// using global::System.Net

var sb = new StringBuilder();
sb.Append("hello");
return new { text = sb.ToString() };
```

Rules (simplified):

- Only namespace imports are supported (no `static`, no aliases).
- The runner scans only the initial header region and stops at the first non-empty, non-comment line.
- Invalid `// using ...` syntax fails the node with a readable error.

## Runner / runtime

This node executes C# in a **separate runner process** (no in-process CLR hosting).

- By default, the node uses a runner shipped inside the npm package.
- On Linux x64/arm64, it auto-detects glibc vs musl and selects the matching self-contained runner.
- On other platforms, build/provide your own runner and set `N8N_CSHARP_RUNNER_PATH`.
  - If you point to a `.dll`, the runtime must have `dotnet` available on `PATH`.

## Security / sandboxing

This node executes **user-provided C# code with no sandboxing**.

Treat access to this node as equivalent to granting arbitrary code execution on the machine/container running n8n:

- The script can read/write files accessible to the n8n process, call the network, and consume CPU/memory.
- There are no built-in isolation guarantees, permissions, or resource limits beyond what your OS/container provides.

Only use this node in trusted environments (trusted workflows + trusted users), and rely on external isolation/hardening (separate instance, container/VM boundaries, least-privilege filesystem/networking, etc.).

## Documentation

Long-lived project notes live in [docs/README.md](docs/README.md).

## Quick start (Docker)

For local testing/dev, you can build and start n8n with the C# node installed:

```bash
docker compose up -d --build
```

Then open:

```text
http://localhost:5678
```

## What’s in this repo

- `n8n-nodes-csharp/` — n8n community node package (TypeScript)
- `runner/N8n.CSharpRunner/` — .NET runner (C#) invoked by the node as a separate process

The node owns the n8n UI + framework integration. It executes C# in a **separate runner process**.

For published releases, the npm package includes self-contained runner executables for Linux (glibc + musl, x64 + arm64), and the node auto-selects the correct one at runtime.

## Build locally

1) Publish the runner into the node package:

Release-like (builds all Linux variants into `n8n-nodes-csharp/runner/<rid>/`):

`node scripts/publish-runner-multi-rid.mjs`

Or build a single self-contained runner for your target (example for Alpine x64):

`dotnet publish runner/N8n.CSharpRunner -c Release -r linux-musl-x64 --self-contained true -o n8n-nodes-csharp/runner/linux-musl-x64`

2) Build the node package:

`cd n8n-nodes-csharp && npm ci && npm run build`

## Docker

Example custom image: `Dockerfile.n8n-csharp` (built on top of `n8nio/n8n:latest`)

Build the image directly:

```bash
docker build -f Dockerfile.n8n-csharp -t n8n-csharp-local:dev .
```

Or run via compose:

```bash
docker compose up -d --build
```

Notes:

- `n8nio/n8n:latest` is Alpine-based; the Dockerfile uses a self-contained Linux/musl runner and runs it in globalization-invariant mode.

## License

MIT (see [LICENSE](LICENSE)).