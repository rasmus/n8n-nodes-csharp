# Docker notes

This repo includes Docker setup for local testing and for building an image that has both:

- n8n
- the custom C# node
- a working runner executable

## Base image and globalization

`n8nio/n8n:latest` is Alpine-based.

The Dockerfile publishes the runner as a **self-contained** Linux/musl executable and runs it with globalization-invariant mode (`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`), so ICU is not required.

The image relies on what is already present in `n8nio/n8n:latest` (no additional `apk add` runtime dependencies).

Runner artifacts are placed under `runner/<rid>/` (for example `runner/linux-musl-x64/`), and the node auto-detects the correct runner on Linux.

## Runtime availability

With a self-contained runner publish, the final image does not need `dotnet` installed.

## Local dev

Build and run via compose:

```bash
docker compose up -d --build
```

Or build the image directly:

```bash
docker build -f Dockerfile.n8n-csharp -t n8n-csharp-local:dev .
```
