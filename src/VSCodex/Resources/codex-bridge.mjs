#!/usr/bin/env node
import readline from 'node:readline';
import { execFileSync, spawn } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import path from 'node:path';

let CodexCtor;
let npmRootGlobal;
async function loadCodex() {
  if (CodexCtor) return CodexCtor;
  const candidates = ['@openai/codex-sdk'];
  if (process.env.CODEX_SDK_PATH) addSdkPathCandidate(candidates, process.env.CODEX_SDK_PATH);
  try {
    const npmRoot = getGlobalNpmRoot();
    if (npmRoot) addSdkPathCandidate(candidates, path.join(npmRoot, '@openai', 'codex-sdk'));
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

function addSdkPathCandidate(candidates, candidatePath) {
  if (!candidatePath) return;
  candidates.push(candidatePath);
  if (/^file:/i.test(candidatePath) || /^[a-z]+:/i.test(candidatePath) && !/^[a-z]:[\\/]/i.test(candidatePath)) return;
  try {
    const packageRoot = path.resolve(candidatePath);
    const packageJson = JSON.parse(readFileSync(path.join(packageRoot, 'package.json'), 'utf8'));
    const exported = packageJson.exports?.['.']?.import ?? packageJson.module ?? packageJson.main ?? 'dist/index.js';
    candidates.push(pathToFileURL(path.join(packageRoot, exported)).href);
  } catch {
    candidates.push(pathToFileURL(path.resolve(candidatePath)).href);
  }
}

function getGlobalNpmRoot() {
  if (npmRootGlobal !== undefined) return npmRootGlobal;
  npmRootGlobal = process.platform === 'win32'
    ? execFileSync('cmd.exe', ['/d', '/s', '/c', 'npm root -g'], { encoding: 'utf8' }).trim()
    : execFileSync('npm', ['root', '-g'], { encoding: 'utf8' }).trim();
  return npmRootGlobal;
}

if (process.argv.includes('--self-test-resilient-parser')) {
  try {
    const state = createCodexEventState();
    processCodexOutputLine('SUCCESS: The process with PID 37220 (child process of PID 26908) has been terminated.', state);
    processCodexOutputLine('{"type":"thread.started","thread_id":"thread-test"}', state);
    processCodexOutputLine('{"type":"codex.rate_limits","plan_type":"prolite","rate_limits":{"allowed":true,"primary":{"used_percent":10,"window_minutes":300,"reset_at":1777735560},"secondary":{"used_percent":34,"window_minutes":10080,"reset_at":1777935600}}}', state);
    processCodexOutputLine('{"type":"item.completed","item":{"type":"agent_message","text":"Hi from parser"}}', state);
    processCodexOutputLine('{"type":"turn.completed","usage":{"input_tokens":1,"output_tokens":2}}', state);
    const result = finalizeCodexEventState(state);
    console.log(JSON.stringify({ threadId: result.threadId, finalResponse: result.finalResponse, usage: result.result.usage, rateLimits: result.result.rateLimits?.rate_limits, ignoredCount: result.result.ignoredStdout.length }));
    process.exit(0);
  } catch (error) {
    console.error(error?.stack ?? String(error));
    process.exit(1);
  }
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
  const options = buildThreadOptions(request);
  if (request.threadId) { if (!threads.has(request.threadId)) threads.set(request.threadId, c.resumeThread(request.threadId, options)); return threads.get(request.threadId); }
  return c.startThread ? c.startThread(options) : await c.thread_start?.(options);
}
function buildThreadOptions(request) {
  const options = {};
  if (request.model) options.model = request.model;
  if (request.reasoningEffort) options.modelReasoningEffort = normalizeLower(request.reasoningEffort);
  if (request.approvalPolicy) options.approvalPolicy = normalizeApprovalPolicy(request.approvalPolicy);
  if (request.sandboxMode) options.sandboxMode = normalizeSandboxMode(request.sandboxMode);
  if (request.workspaceRoot) options.workingDirectory = request.workspaceRoot;
  options.skipGitRepoCheck = true;
  return options;
}
function buildRunOptions(request) {
  const options = { signal: activeAbort.signal };
  return options;
}
function buildInput(request) {
  const images = Array.isArray(request.images) ? request.images.filter(x => x?.kind === 'image' && x.path).map(x => ({ type: 'local_image', path: x.path })) : [];
  return images.length === 0 ? request.prompt : [{ type: 'text', text: request.prompt }, ...images];
}
function normalizeLower(value) { return String(value ?? '').replace(/_/g, '-').toLowerCase(); }
function normalizeApprovalPolicy(value) {
  const text = String(value ?? '').replace(/([a-z])([A-Z])/g, '$1-$2').replace(/_/g, '-').toLowerCase();
  return text === 'onrequest' ? 'on-request' : text === 'onfailure' ? 'on-failure' : text;
}
function normalizeSandboxMode(value) {
  const text = String(value ?? '').replace(/([a-z])([A-Z])/g, '$1-$2').replace(/_/g, '-').toLowerCase();
  return text === 'readonly' ? 'read-only' : text === 'workspacewrite' ? 'workspace-write' : text === 'dangerfullaccess' ? 'danger-full-access' : text;
}
async function handle(request) {
  if (request.command === 'cancel') { activeAbort?.abort?.(); return { cancelled: true }; }
  const thread = await getThread(request);
  activeAbort = new AbortController();
  try {
    const result = await runSdkThread(thread, request);
    const threadId = result?.threadId ?? thread.id ?? request.threadId;
    if (threadId) threads.set(threadId, thread);
    return result;
  } catch (error) {
    if (!isSdkJsonNoiseError(error)) throw error;
    return await runResilientCodexExec(request);
  }
}

async function runSdkThread(thread, request) {
  if (typeof thread.runStreamed === 'function') {
    const state = createCodexEventState(request.threadId);
    const streamed = await thread.runStreamed(buildInput(request), buildRunOptions(request));
    for await (const event of streamed.events) {
      processCodexEventObject(event, state);
    }

    state.threadId = state.threadId ?? thread.id ?? request.threadId;
    return finalizeCodexEventState(state);
  }

  const result = await thread.run(buildInput(request), buildRunOptions(request));
  const threadId = result?.threadId ?? result?.thread_id ?? thread.id ?? request.threadId;
  return { threadId, finalResponse: result?.final_response ?? result?.finalResponse ?? String(result ?? ''), result };
}

function isSdkJsonNoiseError(error) {
  const message = error?.stack ?? error?.message ?? String(error ?? '');
  return message.includes('Failed to parse item: SUCCESS: The process with PID');
}

function resolveCodexExecutable() {
  if (process.env.CODEX_CLI_PATH && existsSync(process.env.CODEX_CLI_PATH)) return process.env.CODEX_CLI_PATH;
  const npmRoot = getGlobalNpmRoot();
  const winNativeCandidates = [
    path.join(npmRoot, '@openai', 'codex', 'node_modules', '@openai', 'codex-win32-x64', 'vendor', 'x86_64-pc-windows-msvc', 'codex', 'codex.exe'),
    path.join(npmRoot, '@openai', 'codex-sdk', 'node_modules', '@openai', 'codex-win32-x64', 'vendor', 'x86_64-pc-windows-msvc', 'codex', 'codex.exe'),
    path.join(npmRoot, '@openai', 'codex-sdk', 'node_modules', '@openai', 'codex', 'node_modules', '@openai', 'codex-win32-x64', 'vendor', 'x86_64-pc-windows-msvc', 'codex', 'codex.exe')
  ];
  if (process.platform === 'win32') {
    for (const candidate of winNativeCandidates) {
      if (existsSync(candidate)) return candidate;
    }
  }

  const winCmd = path.join(path.dirname(npmRoot), 'codex.cmd');
  if (process.platform === 'win32' && existsSync(winCmd)) return winCmd;
  return 'codex';
}

function buildCodexExecArgs(request) {
  const args = ['exec', '--experimental-json'];
  if (request.model) args.push('--model', request.model);
  if (request.approvalPolicy) args.push('--config', 'approval_policy=' + JSON.stringify(normalizeApprovalPolicy(request.approvalPolicy)));
  if (request.reasoningEffort) args.push('--config', 'model_reasoning_effort=' + JSON.stringify(normalizeLower(request.reasoningEffort)));
  if (request.sandboxMode) args.push('--sandbox', normalizeSandboxMode(request.sandboxMode));
  if (request.workspaceRoot) args.push('--cd', request.workspaceRoot);
  args.push('--skip-git-repo-check');
  if (request.threadId) args.push('resume', request.threadId);
  if (Array.isArray(request.images)) {
    for (const image of request.images) {
      if (image?.kind === 'image' && image.path) args.push('--image', image.path);
    }
  }

  args.push('-');
  return args;
}

function isProcessTerminationNoise(line) {
  return /^SUCCESS: The process with PID \d+ .* has been terminated\.$/i.test(String(line ?? '').trim());
}

async function runResilientCodexExec(request) {
  const child = spawn(resolveCodexExecutable(), buildCodexExecArgs(request), {
    cwd: request.workspaceRoot || process.cwd(),
    env: process.env,
    signal: activeAbort?.signal,
    stdio: ['pipe', 'pipe', 'pipe']
  });

  let spawnError = null;
  child.once('error', error => { spawnError = error; });
  const state = createCodexEventState(request.threadId);
  let stderr = '';

  const stdout = readline.createInterface({ input: child.stdout, crlfDelay: Infinity });
  const stderrDone = new Promise(resolve => {
    child.stderr.on('data', data => { stderr += data.toString(); });
    child.stderr.on('end', resolve);
  });
  const exitDone = new Promise(resolve => child.once('close', (code, signal) => resolve({ code, signal })));
  const stdoutDone = (async () => {
    for await (const rawLine of stdout) {
      processCodexOutputLine(rawLine, state);
    }
  })();

  try {
    child.stdin.write(request.prompt ?? '');
    child.stdin.end();
  } catch (error) {
    throw new Error('Codex Exec stdin write failed: ' + (error?.message ?? String(error)));
  }

  await stdoutDone;
  await stderrDone;
  const exit = await exitDone;
  if (spawnError) throw spawnError;
  if (exit.code !== 0 || exit.signal) {
    const reason = exit.signal ? 'signal ' + exit.signal : 'code ' + exit.code;
    throw new Error('Codex Exec exited with ' + reason + ': ' + (stderr || state.ignoredStdout.join('\n')).trim());
  }

  return finalizeCodexEventState(state);
}

function createCodexEventState(threadId) {
  return { threadId, finalResponse: '', usage: null, rateLimits: null, items: [], ignoredStdout: [] };
}

function processCodexOutputLine(rawLine, state) {
  const line = String(rawLine ?? '').trim();
  if (!line || isProcessTerminationNoise(line)) {
    if (line) state.ignoredStdout.push(line);
    return;
  }

  if (!line.trim().startsWith('{')) {
    state.ignoredStdout.push(line);
    return;
  }

  let item;
  try {
    item = JSON.parse(line);
  } catch {
    state.ignoredStdout.push(line);
    return;
  }

  processCodexEventObject(item, state);
}

function processCodexEventObject(item, state) {
  state.items.push(item);
  if (item.type === 'codex.rate_limits' || item.rate_limits || item.rateLimits) state.rateLimits = item;
  if (item.type === 'thread.started') state.threadId = item.thread_id ?? item.threadId ?? state.threadId;
  else if (item.type === 'item.completed' && item.item?.type === 'agent_message') state.finalResponse = item.item.text ?? state.finalResponse;
  else if (item.type === 'turn.completed') state.usage = item.usage ?? state.usage;
  else if (item.type === 'turn.failed') throw new Error(item.error?.message ?? JSON.stringify(item.error ?? item));
}

function finalizeCodexEventState(state) {
  return {
    threadId: state.threadId,
    finalResponse: state.finalResponse,
    result: {
      items: state.items,
      finalResponse: state.finalResponse,
      usage: state.usage,
      rateLimits: state.rateLimits,
      ignoredStdout: state.ignoredStdout
    }
  };
}
const rl = readline.createInterface({ input: process.stdin, crlfDelay: Infinity });
rl.on('line', async line => {
  if (!line.trim()) return;
  let request;
  try { request = JSON.parse(line); const result = await handle(request); emit({ id: request.id, type: 'response', result }); }
  catch (error) { emit({ id: request?.id, type: 'error', message: error?.stack ?? String(error) }); }
});
emit({ type: 'ready', message: 'Codex SDK bridge ready' });
