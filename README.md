# n8n-nodes-csharp — C# Code node for n8n

Run C# inside n8n workflows, similar to n8n’s built-in Code nodes.

This is intended for **self-hosted** n8n (Docker). The node executes your script in a separate `.NET` runner process (spawned via `dotnet`) and exchanges data as JSON over stdin/stdout.

## Quick start (Docker)

Build and start n8n with the C# node installed:

```bash
docker compose up -d --build
```

Then open:

```text
http://localhost:5678
```

## Documentation

Long-lived project notes live in [docs/README.md](docs/README.md).

## Install in self-hosted n8n (manual install)

This follows n8n’s official manual install guide:

```text
https://docs.n8n.io/integrations/community-nodes/installation/manual-install/
```

Important notes for this project:

- This repository is private, and the npm package is published to a private GitHub Packages registry.
- Because of that, the n8n UI-based install (which targets the public npm registry) is typically not suitable.
- You must manually install the package and provide npm authentication to `npm.pkg.github.com`.

### Prerequisites

1) Access to the private GitHub Packages registry

- You need a GitHub token that can read packages from this org/user.
- For GitHub Packages (npm) this is usually a Personal Access Token (PAT) with `read:packages`.
- If your org/repo settings require it, you may also need repository read access for the private repo.

2) `dotnet` available at runtime

The node spawns a `.NET` runner using `dotnet`. Your n8n host/container must have a working `dotnet` runtime on `PATH`.

- If you use this repo’s [Dockerfile.n8n-csharp](Dockerfile.n8n-csharp), `dotnet` is already included.
- If you use the stock `n8nio/n8n` image, you must build your own image that installs/copies `dotnet`.

### Install (Docker)

1) Open a shell in the n8n container (as in the official guide)

```bash
docker exec -it n8n sh
```

2) Create the community nodes folder and go into it

```bash
mkdir -p ~/.n8n/nodes
cd ~/.n8n/nodes
```

3) Configure npm to use GitHub Packages for the `@rasmus` scope

```bash
npm config set @rasmus:registry https://npm.pkg.github.com
```

4) Authenticate to GitHub Packages (private registry)

This writes credentials to `~/.npmrc` inside the container.

```bash
npm config set //npm.pkg.github.com/:_authToken "<YOUR_GITHUB_TOKEN>"
```

5) Install the node package

```bash
npm i @rasmus/n8n-nodes-csharp
```

6) Restart n8n

How you restart depends on how you run n8n. If you use docker compose:

```bash
exit
docker compose restart
```

### Upgrade / uninstall

These are the same commands as the official guide, but using the scoped package name:

- Upgrade:

```bash
docker exec -it n8n sh
cd ~/.n8n/nodes
npm update @rasmus/n8n-nodes-csharp
```

- Uninstall:

```bash
docker exec -it n8n sh
cd ~/.n8n/nodes
npm uninstall @rasmus/n8n-nodes-csharp
```

## Releasing

Publishing happens only from the `release` branch and is intentionally “hard to do by accident”.

The GitHub Actions workflow `.github/workflows/release.yml` will publish the npm package to GitHub Packages only when:

- The commit is on the `release` branch
- The `release` branch HEAD is tagged with `vX.Y.Z`
- `X.Y.Z` matches the version in `n8n-nodes-csharp/package.json`

### Prerequisites

- You have push rights to the repo (and to the `release` branch)
- Your repo’s GitHub Actions are enabled
- Your workflow has `packages: write` permission (already set in this repo)

### Step-by-step: create a release

1) Make sure `release` contains what you want to publish

- Merge your changes into `release` (commonly via PR from `develop` → `release`)
- Ensure CI is green on the commit you intend to release

2) Bump the npm package version

The version that gets published comes from `n8n-nodes-csharp/package.json`.

Option A (recommended, avoids automatic tagging): edit the version manually

- Update `n8n-nodes-csharp/package.json` (and keep `n8n-nodes-csharp/package-lock.json` in sync)

Option B (CLI, updates both files for you):

```bash
cd n8n-nodes-csharp
npm version 0.1.1 --no-git-tag-version
```

3) Commit the version bump on `release`

```bash
git checkout release
git pull

git add n8n-nodes-csharp/package.json n8n-nodes-csharp/package-lock.json
git commit -m "chore(release): v0.1.1"
```

4) Create the required tag on the exact commit you want to publish

The workflow requires the tag to point at `release` HEAD.

```bash
git tag v0.1.1
```

If you prefer annotated tags:

```bash
git tag -a v0.1.1 -m "v0.1.1"
```

5) Push the branch and the tag

```bash
git push origin release
git push origin v0.1.1
```

6) Watch the GitHub Actions release workflow

- Go to the Actions tab and open the latest run of “Release”
- If it fails immediately with a message about tags:
    - Confirm the tag name matches exactly (`v${version}`)
    - Confirm the tag points at HEAD: `git tag --points-at HEAD`

7) Verify the package exists in GitHub Packages

Once the workflow succeeds, the package should be available as:

- `@rasmus/n8n-nodes-csharp@0.1.1`

You can verify from a machine with GitHub Packages access:

```bash
npm view @rasmus/n8n-nodes-csharp@0.1.1 version
```

### What gets published

- Only the npm package is published (not a container image)
- The npm package includes the compiled .NET runner under `runner/` so the node works “out of the box” after install
- At runtime, n8n still needs `dotnet` available on the PATH (the provided Dockerfile/compose setup includes it)

## Using the C# Code node

The node supports two execution modes:

- **All Items**: run once; use `Items`
- **Per Item**: run once per input item; use `Item` + `Index`

Return shapes:

- return an object → one output item
- return an array of objects → multiple output items
- return a scalar → wrapped as `{ "value": ... }`

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

Example: return scalars (wrapped as `{ "value": ... }`):

```csharp
return Items!.Select(_ => 1).ToArray();
```

More examples and details live in `n8n-nodes-csharp/README.md`.

## What’s in this repo

- `n8n-nodes-csharp/` — n8n community node package (TypeScript)
- `runner/N8n.CSharpRunner/` — .NET runner (C#) invoked by the node as a separate process

The node owns the n8n UI + framework integration. It executes C# in a **separate `dotnet` process**.

For published releases, the npm package includes self-contained runner executables for Linux (glibc + musl, x64 + arm64), and the node auto-selects the correct one at runtime.

## Build locally

1) Publish the runner into the node package:

Framework-dependent (requires `dotnet` installed at runtime):

`dotnet publish runner/N8n.CSharpRunner -c Release -o n8n-nodes-csharp/runner`

Self-contained for Alpine (no `dotnet` at runtime):

`dotnet publish runner/N8n.CSharpRunner -c Release -r linux-musl-x64 --self-contained true -o n8n-nodes-csharp/runner`

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

```
MIT License

Copyright (c) 2025 Rasmus Mikkelsen

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```