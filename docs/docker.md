# Docker notes

This repo includes Docker setup for local testing and for building an image that has both:

- n8n
- the custom C# node
- a working `dotnet` runtime for the runner

## Base image and ICU

`n8nio/n8n:latest` is Alpine-based.

.NET on Alpine typically requires ICU libraries for globalization support, so the Dockerfile installs:

- `icu-libs`

If ICU is missing, the runner may fail at runtime.

## `dotnet` availability

The C# node spawns the runner using the `dotnet` command.

The Dockerfile ensures:

- the .NET runtime exists in the image
- `dotnet` is available on PATH (including a `/usr/bin/dotnet` symlink to avoid PATH quirks)

## Local dev

Build and run via compose:

```bash
docker compose up -d --build
```

Or build the image directly:

```bash
docker build -f Dockerfile.n8n-csharp -t n8n-csharp-local:dev .
```
