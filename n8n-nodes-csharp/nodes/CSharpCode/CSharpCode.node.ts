import { spawn } from 'node:child_process';
import * as fs from 'node:fs';
import * as path from 'node:path';

import type {
  IDataObject,
  IExecuteFunctions,
  INodeExecutionData,
  INodeType,
  INodeTypeDescription,
} from 'n8n-workflow';

type ExecutionMode = 'allItems' | 'perItem';

type RunnerResponse =
  | {
      ok: true;
      items: unknown[];
    }
  | {
      ok: false;
      error: {
        message: string;
        detail?: string;
      };
    };

export class CSharpCode implements INodeType {
  description: INodeTypeDescription = {
    displayName: 'C# Code',
    name: 'csharpCode',
    icon: 'file:csharp.svg',
    group: ['transform'],
    version: 1,
    description: 'Run custom C# code',
    defaults: {
      name: 'C# Code',
    },
    inputs: ['main'],
    outputs: ['main'],
    properties: [
      {
        displayName: 'Mode',
        name: 'mode',
        type: 'options',
        default: 'allItems',
        options: [
          {
            name: 'All Items',
            value: 'allItems',
          },
          {
            name: 'Per Item',
            value: 'perItem',
          },
        ],
      },
      {
        displayName: 'Timeout (ms)',
        name: 'timeoutMs',
        type: 'number',
        default: 30000,
        description: 'Maximum execution time before the runner process is killed',
        typeOptions: {
          minValue: 0,
        },
      },
      {
        displayName: 'Code',
        name: 'code',
        type: 'string',
        default: 'return Items;\n',
        description:
          'C# script. Available globals: Items (array), Item (object), Index (number). Return an object or array of objects.',
        typeOptions: {
          editor: 'codeNodeEditor',
          editorLanguage: 'csharp',
          rows: 12,
        },
      },
    ],
  };

  async execute(this: IExecuteFunctions): Promise<INodeExecutionData[][]> {
    const items = this.getInputData();

    const mode = this.getNodeParameter('mode', 0) as ExecutionMode;
    const timeoutMs = this.getNodeParameter('timeoutMs', 0) as number;
    const code = this.getNodeParameter('code', 0) as string;

    const inputJson = items.map((item) => item.json ?? {});
    const runnerPath = resolveRunnerPath();

    const requestPayload = {
      mode,
      items: inputJson,
      code,
    };

    const raw = await runDotnetRunner({
      runnerPath,
      payload: requestPayload,
      timeoutMs,
    });

    let response: RunnerResponse;
    try {
      response = JSON.parse(raw) as RunnerResponse;
    } catch (error) {
      throw new Error(
        `C# runner returned non-JSON output. Set N8N_CSHARP_RUNNER_PATH correctly. Output: ${truncate(
          raw,
          2000,
        )}`,
      );
    }

    if (!response || typeof response !== 'object' || !('ok' in response)) {
      throw new Error(
        `C# runner returned unexpected response: ${truncate(JSON.stringify(response), 2000)}`,
      );
    }

    if (response.ok === false) {
      const msg = response.error?.message ?? 'Unknown C# execution error';
      const detail = response.error?.detail ? `\n\n${response.error.detail}` : '';
      throw new Error(`${msg}${detail}`);
    }

    const outItems: INodeExecutionData[] = response.items.map((json) => ({
      json: coerceToJsonObject(json),
    }));
    return [outItems];
  }
}

function resolveRunnerPath(): string {
  if (process.env.N8N_CSHARP_RUNNER_PATH) {
    return process.env.N8N_CSHARP_RUNNER_PATH;
  }

  const runnerDir = path.join(__dirname, '..', '..', '..', 'runner');

  // New layout: ship multiple self-contained runner builds under runner/<rid>/.
  // This enables running on both glibc and musl containers and on x64/arm64.
  const rid = detectLinuxRid();
  if (rid) {
    const exeName = process.platform === 'win32' ? 'N8n.CSharpRunner.exe' : 'N8n.CSharpRunner';
    const ridExe = path.join(runnerDir, rid, exeName);
    if (fs.existsSync(ridExe)) return ridExe;
  }

  // Prefer an executable runner if shipped alongside the package.
  // This enables using self-contained publishes (no `dotnet` required at runtime).
  const candidates = [
    path.join(runnerDir, 'N8n.CSharpRunner'),
    path.join(runnerDir, 'N8n.CSharpRunner.exe'),
    path.join(runnerDir, 'N8n.CSharpRunner.dll'),
  ];

  for (const candidate of candidates) {
    if (fs.existsSync(candidate)) return candidate;
  }

  // Fallback to the historical default for clearer error messages.
  return candidates[candidates.length - 1];
}

function detectLinuxRid(): string | null {
  if (process.platform !== 'linux') return null;

  const arch = process.arch;
  if (arch !== 'x64' && arch !== 'arm64') return null;

  const isMusl = detectMusl(arch);
  return isMusl ? `linux-musl-${arch}` : `linux-${arch}`;
}

function detectMusl(arch: 'x64' | 'arm64'): boolean {
  // Heuristic 1: Node's process.report includes glibc runtime version when running on glibc.
  // If present, we assume glibc.
  try {
    const report = (process as any).report?.getReport?.();
    const glibcVersion = report?.header?.glibcVersionRuntime;
    if (typeof glibcVersion === 'string' && glibcVersion.length > 0) return false;
  } catch {
    // ignore
  }

  // Heuristic 2: musl loader exists in typical Alpine paths.
  const muslLoader =
    arch === 'x64' ? '/lib/ld-musl-x86_64.so.1' : '/lib/ld-musl-aarch64.so.1';
  if (fs.existsSync(muslLoader)) return true;

  // Default to glibc if uncertain.
  return false;
}

async function runDotnetRunner(options: {
  runnerPath: string;
  payload: unknown;
  timeoutMs: number;
}): Promise<string> {
  const { runnerPath, payload, timeoutMs } = options;

  const { command, args } = resolveRunnerCommand(runnerPath);

  const child = spawn(command, args, {
    stdio: ['pipe', 'pipe', 'pipe'],
    env: {
      ...process.env,
      DOTNET_CLI_TELEMETRY_OPTOUT: '1',
      DOTNET_NOLOGO: '1',
      // Avoid ICU/globalization dependency in minimal containers (e.g. Alpine).
      // Safe to set even when ICU is present.
      DOTNET_SYSTEM_GLOBALIZATION_INVARIANT: '1',
    },
  });

  const stdout: Buffer[] = [];
  const stderr: Buffer[] = [];

  child.stdout.on('data', (d: Buffer) => stdout.push(d));
  child.stderr.on('data', (d: Buffer) => stderr.push(d));

  let timeoutHandle: NodeJS.Timeout | undefined;
  if (timeoutMs > 0) {
    timeoutHandle = setTimeout(() => {
      child.kill('SIGKILL');
    }, timeoutMs);
  }

  child.stdin.write(JSON.stringify(payload));
  child.stdin.end();

  const exit = await new Promise<{ code: number | null; signal: NodeJS.Signals | null }>(
    (resolve, reject) => {
    child.on('error', reject);
    child.on('close', (code, signal) => resolve({ code, signal }));
  });

  if (timeoutHandle) clearTimeout(timeoutHandle);

  const out = Buffer.concat(stdout).toString('utf8').trim();
  const err = Buffer.concat(stderr).toString('utf8').trim();

  if (exit.code !== 0) {
    const exitInfo = exit.code === null ? `signal ${exit.signal ?? 'unknown'}` : `code ${exit.code}`;
    throw new Error(
      `C# runner exited with ${exitInfo}. stderr: ${truncate(err, 4000)}\nstdout: ${truncate(
        out,
        4000,
      )}`,
    );
  }

  return out;
}

function resolveRunnerCommand(runnerPath: string): { command: string; args: string[] } {
  // Backwards compatible: default runner is a framework-dependent DLL.
  if (runnerPath.toLowerCase().endsWith('.dll')) {
    return { command: 'dotnet', args: [runnerPath] };
  }

  // Self-contained publish (or apphost) executable.
  return { command: runnerPath, args: [] };
}

function coerceToJsonObject(value: unknown): IDataObject {
  if (value && typeof value === 'object' && !Array.isArray(value)) {
    return value as IDataObject;
  }

  return { value: value as any } as IDataObject;
}

function truncate(s: string, max: number): string {
  if (s.length <= max) return s;
  return s.slice(0, max) + '…';
}
