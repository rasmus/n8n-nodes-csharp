import { copyFile, mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const thisFile = fileURLToPath(import.meta.url);
const thisDir = path.dirname(thisFile);

// Repo layout:
//   <repo>/README.md
//   <repo>/LICENSE
//   <repo>/n8n-nodes-csharp/scripts/copy-package-docs.mjs  (this file)
const repoRootDir = path.resolve(thisDir, '..', '..');
const packageDir = path.resolve(thisDir, '..');

const copies = [
	{ src: path.join(repoRootDir, 'README.md'), dst: path.join(packageDir, 'README.md') },
	{ src: path.join(repoRootDir, 'LICENSE'), dst: path.join(packageDir, 'LICENSE') },
];

await mkdir(packageDir, { recursive: true });

for (const { src, dst } of copies) {
	await copyFile(src, dst);
}
