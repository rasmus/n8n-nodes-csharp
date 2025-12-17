import { execFileSync } from 'node:child_process';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const runnerProject = path.join(repoRoot, 'runner', 'N8n.CSharpRunner');
const outRoot = path.join(repoRoot, 'n8n-nodes-csharp', 'runner');

const rids = ['linux-x64', 'linux-arm64', 'linux-musl-x64', 'linux-musl-arm64'];

async function rmrf(p) {
  await fs.rm(p, { recursive: true, force: true });
}

async function main() {
  await rmrf(outRoot);
  await fs.mkdir(outRoot, { recursive: true });

  for (const rid of rids) {
    const outDir = path.join(outRoot, rid);
    await fs.mkdir(outDir, { recursive: true });

    // Important: do NOT use PublishSingleFile here.
    // Roslyn scripting uses Assembly.Location for metadata references,
    // which is not supported in single-file bundled apps.
    execFileSync(
      'dotnet',
      [
        'publish',
        runnerProject,
        '-c',
        'Release',
        '-r',
        rid,
        '--self-contained',
        'true',
        '-o',
        outDir,
      ],
      { stdio: 'inherit' },
    );
  }
}

await main();
