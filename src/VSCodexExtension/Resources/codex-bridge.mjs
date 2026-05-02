#!/usr/bin/env node
import readline from 'node:readline';
import { execFileSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';
import path from 'node:path';

let CodexCtor;
async function loadCodex() {
  if (CodexCtor) return CodexCtor;
  const candidates = ['@openai/codex-sdk', process.env.CODEX_SDK_PATH].filter(Boolean);
  try {
    const npmRoot = execFileSync(process.platform === 'win32' ? 'npm.cmd' : 'npm', ['root', '-g'], { encoding: 'utf8' }).trim();
    if (npmRoot) candidates.push(pathToFileURL(path.join(npmRoot, '@openai', 'codex-sdk')).href);
  } catch {}
  let lastError;
  for (const candidate of candidates) {
    try {
      const module = await import(candidate);
      CodexCtor = module.Codex ?? module.default?.Codex ?? module.default;
      if (CodexCtor) return CodexCtor;
    } catch (error) { lastError = error; }
  }
  throw lastError ?? new Error('Unable to import @openai/codex-sdk. Install with: npm install -g @openai/codex-sdk');
}

if (process.argv.includes('--check')) {
  try {
    await loadCodex();
    console.log('Codex SDK bridge prerequisites OK');
    process.exit(0);
  } catch (error) {
    console.error(error?.stack ?? String(error));
    console.error('Install on Windows with: npm install -g @openai/codex-sdk');
    process.exit(1);
  }
}

const threads = new Map();
let codex;
let activeAbort;
function emit(value) { process.stdout.write(JSON.stringify(value) + '\n'); }
async function ensureCodex() { if (codex) return codex; const Codex = await loadCodex(); codex = new Codex(); return codex; }
async function getThread(request) {
  const c = await ensureCodex();
  if (request.threadId) { if (!threads.has(request.threadId)) threads.set(request.threadId, c.resumeThread(request.threadId)); return threads.get(request.threadId); }
  const options = {};
  if (request.model) options.model = request.model;
  if (request.profile) options.profile = request.profile;
  return c.startThread ? c.startThread(options) : await c.thread_start?.(options);
}
function buildRunOptions(request) {
  const options = { signal: activeAbort.signal };
  if (request.model) options.model = request.model;
  if (request.reasoningEffort) options.reasoningEffort = request.reasoningEffort;
  if (request.verbosity) options.verbosity = request.verbosity;
  if (request.serviceTier) options.serviceTier = request.serviceTier;
  if (request.approvalPolicy) options.approvalPolicy = request.approvalPolicy;
  if (request.sandboxMode) options.sandboxMode = request.sandboxMode;
  if (request.workspaceRoot) options.cwd = request.workspaceRoot;
  if (Array.isArray(request.images)) options.images = request.images.filter(x => x?.kind === 'image').map(x => x.path);
  return options;
}
async function handle(request) {
  if (request.command === 'cancel') { activeAbort?.abort?.(); return { cancelled: true }; }
  const thread = await getThread(request);
  activeAbort = new AbortController();
  const result = await thread.run(request.prompt, buildRunOptions(request));
  const threadId = result?.threadId ?? result?.thread_id ?? thread.id ?? request.threadId;
  if (threadId) threads.set(threadId, thread);
  return { threadId, finalResponse: result?.final_response ?? result?.finalResponse ?? String(result ?? ''), result };
}
const rl = readline.createInterface({ input: process.stdin, crlfDelay: Infinity });
rl.on('line', async line => {
  if (!line.trim()) return;
  let request;
  try { request = JSON.parse(line); const result = await handle(request); emit({ id: request.id, type: 'response', result }); }
  catch (error) { emit({ id: request?.id, type: 'error', message: error?.stack ?? String(error) }); }
});
emit({ type: 'ready', message: 'Codex SDK bridge ready' });
