# Releasing

Releases publish the npm package to GitHub Packages.

## Rules (enforced by CI)

A release publish happens only when:

- The commit is on the `release` branch
- The `release` branch HEAD is tagged with `vX.Y.Z`
- `X.Y.Z` matches the version in `n8n-nodes-csharp/package.json`

## Step-by-step

1) Merge changes into `release`

- Ensure CI is green.

2) Bump `n8n-nodes-csharp` version

Either edit `n8n-nodes-csharp/package.json` manually (and keep the lockfile in sync), or run:

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

4) Create tag `v0.1.1` on `release` HEAD

```bash
git tag v0.1.1
```

5) Push branch + tag

```bash
git push origin release
git push origin v0.1.1
```

6) Verify publish

- Check the GitHub Actions “Release” workflow run.
- Confirm the package appears under GitHub Packages as `@rasmus/n8n-nodes-csharp@0.1.1`.

## What gets published

- Only the npm package (not Docker images)
- The npm package includes the compiled .NET runner under `runner/`

## Common failure: missing or wrong tag

If the release workflow fails early:

- Confirm the tag matches exactly `v${version}` from `n8n-nodes-csharp/package.json`.
- Confirm the tag points at HEAD:

```bash
git tag --points-at HEAD
```
