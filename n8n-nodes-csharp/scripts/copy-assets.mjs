import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const pkgRoot = path.resolve(scriptDir, '..');
const srcNodes = path.join(pkgRoot, 'nodes');
const distNodes = path.join(pkgRoot, 'dist', 'nodes');

async function* walk(dir) {
  const entries = await fs.readdir(dir, { withFileTypes: true });
  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      yield* walk(fullPath);
    } else {
      yield fullPath;
    }
  }
}

async function main() {
  await fs.mkdir(distNodes, { recursive: true });

  for await (const filePath of walk(srcNodes)) {
    if (!filePath.endsWith('.svg')) continue;
    const rel = path.relative(srcNodes, filePath);
    const dest = path.join(distNodes, rel);
    await fs.mkdir(path.dirname(dest), { recursive: true });
    await fs.copyFile(filePath, dest);
  }
}

await main();
