import { spawn } from 'node:child_process';
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

  return path.join(__dirname, '..', '..', '..', 'runner', 'N8n.CSharpRunner.dll');
}

async function runDotnetRunner(options: {
  runnerPath: string;
  payload: unknown;
  timeoutMs: number;
}): Promise<string> {
  const { runnerPath, payload, timeoutMs } = options;

  const child = spawn('dotnet', [runnerPath], {
    stdio: ['pipe', 'pipe', 'pipe'],
    env: {
      ...process.env,
      DOTNET_CLI_TELEMETRY_OPTOUT: '1',
      DOTNET_NOLOGO: '1',
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
