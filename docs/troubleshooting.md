# Troubleshooting

## Node error: “C# runner returned non-JSON output”

This error comes from the n8n node when it fails to parse the runner stdout as JSON.

Things to check:

- The runner path is correct.
  - On Linux x64/arm64, the node auto-selects a packaged runner under `runner/linux-*/` or `runner/linux-musl-*/`.
  - To override, set `N8N_CSHARP_RUNNER_PATH` to an absolute path to the runner (`N8n.CSharpRunner` or `N8n.CSharpRunner.dll`).
- The runner must write JSON to stdout.
  - Any logging in scripts should use `Console.Error.WriteLine(...)`.

## Container issue: ICU / globalization errors

If the runner fails with errors referencing ICU / globalization:

- Either install `icu-libs` (Alpine) or ensure the runner is executed with `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`.

## Container issue: `dotnet` not found

If the node fails because `dotnet` can’t be executed:

- If you are using a `.dll` runner, ensure the n8n container includes the .NET runtime and `dotnet` is on PATH.
- Prefer using a self-contained runner executable to avoid the `dotnet` runtime dependency.

## Runner issue: “node already has a parent” / JsonNode parent exceptions

Cause:

- `System.Text.Json.Nodes.JsonNode` instances can only belong to one parent.
- When a script returns an array, elements already belong to that array.

Fix:

- The runner deep-clones nodes during normalization before wrapping/returning them.

## Install failures

If the node doesn’t show up after installing, or `npm i` fails in a manual install:

- Ensure community nodes are enabled in your n8n deployment (see n8n docs).
- Ensure you installed the correct module name: `@madoere/n8n-nodes-csharp`.
- Restart n8n after install.
