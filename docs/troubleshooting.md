# Troubleshooting

## Node error: “C# runner returned non-JSON output”

This error comes from the n8n node when it fails to parse the runner stdout as JSON.

Things to check:

- The runner path is correct.
  - Set `N8N_CSHARP_RUNNER_PATH` to an absolute path to `N8n.CSharpRunner.dll`.
- The runner must write JSON to stdout.
  - Any logging in scripts should use `Console.Error.WriteLine(...)`.

## Container issue: ICU / globalization errors

If the runner fails with errors referencing ICU / globalization:

- Ensure the image includes `icu-libs` (Alpine).

## Container issue: `dotnet` not found

If the node fails because `dotnet` can’t be executed:

- Ensure the n8n container includes the .NET runtime.
- Ensure `dotnet` is on PATH (the Dockerfile adds a `/usr/bin/dotnet` symlink).

## Runner issue: “node already has a parent” / JsonNode parent exceptions

Cause:

- `System.Text.Json.Nodes.JsonNode` instances can only belong to one parent.
- When a script returns an array, elements already belong to that array.

Fix:

- The runner deep-clones nodes during normalization before wrapping/returning them.

## Private GitHub Packages install failures

Common causes:

- Missing npm auth token for `npm.pkg.github.com`.
- Token lacks `read:packages`.
- Org/repo requires additional permissions for private packages.

Recommended approach:

- Configure `@rasmus` registry and auth token inside the container before `npm i`.
