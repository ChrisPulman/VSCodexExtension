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
let appServer;
let appServerSequence = 0;
const activeAppServerTurns = new Map();
const earlyTurnCompletions = new Map();
function emit(value) { process.stdout.write(JSON.stringify(value) + '\n'); }
async function ensureCodex() { if (codex) return codex; const Codex = await loadCodex(); codex = new Codex(); return codex; }
async function getThread(request) {
  const c = await ensureCodex();
  const options = buildThreadOptions(request);
  if (request.threadId) {
    const cached = threads.get(request.threadId);
    if (cached && cached.workspaceRoot === request.workspaceRoot) return cached.thread;
    const thread = c.resumeThread(request.threadId, options);
    threads.set(request.threadId, { thread, workspaceRoot: request.workspaceRoot });
    return thread;
  }
  return c.startThread ? c.startThread(options) : await c.thread_start?.(options);
}
function buildThreadOptions(request) {
  const { model, effort } = normalizeModelAndReasoningEffort(request);
  const options = {};
  if (model) options.model = model;
  options.modelReasoningEffort = effort;
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
  if (request.command === 'cancel') return await interruptAppServerTurn(request.threadId);
  if (request.command === 'interrupt') return await interruptAppServerTurn(request.threadId);
  if (request.command === 'steer') return await steerAppServerTurn(request);
  if (request.command === 'respondServerRequest') return respondToAppServerRequest(request);
  if (request.command === 'getRateLimits') return await getRateLimits();
  if (!request.workspaceRoot) throw new Error('VSCodex workspaceRoot is required. Wait for Visual Studio to finish loading a solution or project before running Codex.');
  try {
    return await runAppServerTurn(request);
  } catch (error) {
    emit({ type: 'transport-fallback', message: 'Codex app-server transport failed; trying the SDK bridge.', detail: error?.message ?? String(error) });
    const thread = await getThread(request);
    activeAbort = new AbortController();
    try {
      const result = await runSdkThread(thread, request);
      const threadId = result?.threadId ?? thread.id ?? request.threadId;
      if (threadId) threads.set(threadId, { thread, workspaceRoot: request.workspaceRoot });
      return result;
    } catch (sdkError) {
      if (!isSdkJsonNoiseError(sdkError)) throw sdkError;
      return await runResilientCodexExec(request);
    }
  }
}

async function ensureAppServer() {
  if (appServer?.child && !appServer.child.killed && appServer.child.exitCode == null) return appServer;
  const child = spawn(resolveCodexExecutable(), ['app-server', '--listen', 'stdio://'], {
    env: process.env,
    stdio: ['pipe', 'pipe', 'pipe']
  });
  let stderr = '';
  child.stderr.on('data', data => {
    const message = data.toString().trim();
    if (!message) return;
    stderr += message + '\n';
  });
  const rpc = createJsonRpcClient(child, 30000, processAppServerMessage);
  const initialize = await rpc.send({
    jsonrpc: '2.0',
    id: nextAppServerId(),
    method: 'initialize',
    params: {
      clientInfo: { name: 'VSCodex', title: 'VSCodex for Visual Studio', version: '0.5.0' },
      capabilities: { experimentalApi: true }
    }
  });
  if (initialize.error) {
    rpc.close();
    try { child.kill(); } catch {}
    throw new Error('Codex app-server initialization failed: ' + JSON.stringify(initialize.error) + (stderr ? '\n' + stderr.trim() : ''));
  }
  appServer = { child, rpc };
  return appServer;
}

function nextAppServerId() {
  appServerSequence += 1;
  return 'vscodex-' + appServerSequence;
}

async function runAppServerTurn(request) {
  const server = await ensureAppServer();
  const threadResponse = request.threadId
    ? await server.rpc.send({
        jsonrpc: '2.0',
        id: nextAppServerId(),
        method: 'thread/resume',
        params: buildThreadResumeParams(request)
      })
    : await server.rpc.send({
        jsonrpc: '2.0',
        id: nextAppServerId(),
        method: 'thread/start',
        params: buildThreadStartParams(request)
      });
  throwOnRpcError(threadResponse, request.threadId ? 'resume thread' : 'start thread');
  const threadId = threadResponse.result?.thread?.id ?? request.threadId;
  if (!threadId) throw new Error('Codex app-server returned no thread identifier.');

  const turnResponse = await server.rpc.send({
    jsonrpc: '2.0',
    id: nextAppServerId(),
    method: 'turn/start',
    params: buildTurnStartParams(request, threadId)
  });
  throwOnRpcError(turnResponse, 'start turn');
  const turnId = turnResponse.result?.turn?.id;
  if (!turnId) throw new Error('Codex app-server returned no turn identifier.');

  const completion = createDeferred();
  const state = {
    threadId,
    turnId,
    operationId: request.operationId ?? turnId,
    finalResponse: '',
    pendingDelta: '',
    deltaTimer: null,
    items: [],
    completion
  };
  activeAppServerTurns.set(threadId, state);
  emit({ type: 'turn-started', message: 'Codex is working...', threadId, turnId, operationId: state.operationId });

  const earlyCompletion = earlyTurnCompletions.get(turnId);
  if (earlyCompletion) {
    earlyTurnCompletions.delete(turnId);
    completeAppServerTurn(earlyCompletion);
  }

  return await completion.promise;
}

function buildThreadStartParams(request) {
  const { model } = normalizeModelAndReasoningEffort(request);
  const params = {
    cwd: request.workspaceRoot,
    runtimeWorkspaceRoots: [request.workspaceRoot],
    approvalPolicy: normalizeApprovalPolicy(request.approvalPolicy),
    sandbox: normalizeSandboxMode(request.sandboxMode),
    serviceName: 'VSCodex',
    ephemeral: false
  };
  if (model) params.model = model;
  const serviceTier = normalizeServiceTier(request.serviceTier);
  if (serviceTier) params.serviceTier = serviceTier;
  return params;
}

function buildThreadResumeParams(request) {
  const { model } = normalizeModelAndReasoningEffort(request);
  const params = {
    threadId: request.threadId,
    cwd: request.workspaceRoot,
    runtimeWorkspaceRoots: [request.workspaceRoot],
    approvalPolicy: normalizeApprovalPolicy(request.approvalPolicy),
    sandbox: normalizeSandboxMode(request.sandboxMode)
  };
  if (model) params.model = model;
  const serviceTier = normalizeServiceTier(request.serviceTier);
  if (serviceTier) params.serviceTier = serviceTier;
  return params;
}

function buildTurnStartParams(request, threadId) {
  const { model, effort } = normalizeModelAndReasoningEffort(request);
  const input = [{ type: 'text', text: request.prompt ?? '', text_elements: [] }];
  if (Array.isArray(request.images)) {
    for (const image of request.images) {
      if (image?.kind === 'image' && image.path) input.push({ type: 'localImage', path: image.path });
    }
  }
  const params = {
    threadId,
    input,
    cwd: request.workspaceRoot,
    runtimeWorkspaceRoots: [request.workspaceRoot],
    approvalPolicy: normalizeApprovalPolicy(request.approvalPolicy),
    model,
    serviceTier: normalizeServiceTier(request.serviceTier),
    effort
  };
  return Object.fromEntries(Object.entries(params).filter(([, value]) => value !== undefined && value !== ''));
}

const reasoningEffortOrder = ['low', 'medium', 'high', 'xhigh', 'max', 'ultra'];
const solTerraModels = new Set(['gpt-5.6-sol', 'gpt-5.6-terra']);
const lunaModels = new Set(['gpt-5.6-luna']);
const standardReasoningModels = new Set(['gpt-5.5', 'gpt-5.4', 'gpt-5.4-mini', 'gpt-5.3-codex-spark']);

function normalizeModel(value) {
  const normalized = normalizeLower(value).trim();
  return normalized || undefined;
}

function getSupportedReasoningEfforts(model) {
  if (solTerraModels.has(model)) return reasoningEffortOrder;
  if (lunaModels.has(model)) return reasoningEffortOrder.slice(0, -1);
  if (standardReasoningModels.has(model)) return reasoningEffortOrder.slice(0, 4);
  return ['medium'];
}

function normalizeReasoningEffort(model, value) {
  const supported = getSupportedReasoningEfforts(model);
  const normalized = normalizeLower(value).trim();
  if (supported.includes(normalized)) return normalized;
  const requestedRank = reasoningEffortOrder.indexOf(normalized);
  return requestedRank > reasoningEffortOrder.indexOf(supported.at(-1)) ? supported.at(-1) : 'medium';
}

function normalizeModelAndReasoningEffort(request) {
  const model = normalizeModel(request.model);
  return { model, effort: normalizeReasoningEffort(model, request.reasoningEffort) };
}

function normalizeServiceTier(value) {
  const normalized = normalizeLower(value);
  return normalized && normalized !== 'auto' ? normalized : undefined;
}

if (process.argv.includes('--self-test-model-catalog')) {
  try {
    const cases = [
      ['gpt-5.6-sol', 'ultra', 'ultra'],
      ['gpt-5.6-terra', 'max', 'max'],
      ['gpt-5.6-luna', 'ultra', 'max'],
      ['gpt-5.5', 'max', 'xhigh'],
      ['gpt-5.4-mini', 'ultra', 'xhigh'],
      ['gpt-5.3-codex-spark', 'max', 'xhigh'],
      ['gpt-5.6-sol', 'minimal', 'medium'],
      ['custom-provider-model', 'ultra', 'medium']
    ];
    const resolved = cases.map(([model, requested, expected]) => {
      const actual = normalizeReasoningEffort(model, requested);
      if (actual !== expected) throw new Error(`${model}/${requested} resolved to ${actual}; expected ${expected}.`);
      return { model, requested, actual };
    });
    const request = { model: 'gpt-5.6-luna', reasoningEffort: 'ultra', workspaceRoot: process.cwd(), prompt: 'test' };
    const thread = buildThreadOptions(request);
    const turn = buildTurnStartParams(request, 'thread-test');
    const execArgs = buildCodexExecArgs(request);
    if (thread.modelReasoningEffort !== 'max' || turn.effort !== 'max' || !execArgs.includes('model_reasoning_effort="max"')) {
      throw new Error('Model/effort normalization diverged between the SDK, app-server, and CLI transports.');
    }
    console.log(JSON.stringify({ resolved, transportEffort: turn.effort }));
    process.exit(0);
  } catch (error) {
    console.error(error?.stack ?? String(error));
    process.exit(1);
  }
}

async function steerAppServerTurn(request) {
  if (!request.threadId) throw new Error('A thread identifier is required to steer a Codex turn.');
  if (!request.prompt?.trim()) throw new Error('A steering prompt is required.');
  const active = activeAppServerTurns.get(request.threadId);
  if (!active) throw new Error('The selected chat has no active Codex turn to steer.');
  const server = await ensureAppServer();
  const response = await server.rpc.send({
    jsonrpc: '2.0',
    id: nextAppServerId(),
    method: 'turn/steer',
    params: {
      threadId: active.threadId,
      expectedTurnId: active.turnId,
      input: [{ type: 'text', text: request.prompt, text_elements: [] }]
    }
  });
  throwOnRpcError(response, 'steer turn');
  emit({ type: 'turn-steered', message: 'Guidance added to the active Codex turn.', threadId: active.threadId, turnId: active.turnId });
  return { threadId: active.threadId, turnId: response.result?.turnId ?? active.turnId, steered: true };
}

async function interruptAppServerTurn(threadId) {
  if (!threadId) {
    activeAbort?.abort?.();
    const activeTurns = Array.from(activeAppServerTurns.values());
    await Promise.all(activeTurns.map(active => interruptAppServerTurn(active.threadId)));
    return { interrupted: activeTurns.length > 0 };
  }
  const active = activeAppServerTurns.get(threadId);
  if (!active) return { threadId, interrupted: false };
  const server = await ensureAppServer();
  const response = await server.rpc.send({
    jsonrpc: '2.0',
    id: nextAppServerId(),
    method: 'turn/interrupt',
    params: { threadId: active.threadId, turnId: active.turnId }
  });
  throwOnRpcError(response, 'interrupt turn');
  emit({ type: 'turn-interrupted', message: 'Codex turn interrupted.', threadId: active.threadId, turnId: active.turnId });
  return { threadId: active.threadId, turnId: active.turnId, interrupted: true };
}

function respondToAppServerRequest(request) {
  if (!request.requestId) throw new Error('A server request identifier is required.');
  if (!appServer?.rpc) throw new Error('Codex app-server is not running.');
  return appServer.rpc.respond(request.requestId, request.result ?? { decision: 'decline' });
}

function processAppServerMessage(item) {
  if (!item?.method) return;
  if (item.id !== undefined && item.id !== null) {
    emit({
      type: 'approval-request',
      message: describeServerRequest(item),
      requestId: item.id,
      method: item.method,
      threadId: item.params?.threadId,
      turnId: item.params?.turnId,
      params: item.params
    });
    return;
  }

  const params = item.params ?? {};
  const turnId = params.turnId ?? params.turn?.id;
  const threadId = params.threadId;
  const active = threadId ? activeAppServerTurns.get(threadId) : findActiveTurn(turnId);
  if (item.method === 'item/agentMessage/delta' && active) {
    const delta = params.delta ?? '';
    active.finalResponse += delta;
    queueAssistantDelta(active, delta);
    return;
  } else if (item.method === 'item/completed' && active) {
    active.items.push(params.item);
    if (params.item?.type === 'agentMessage' && params.item.text) active.finalResponse = params.item.text;
  } else if (item.method === 'turn/completed') {
    if (active) completeAppServerTurn({ item, active });
    else if (turnId) earlyTurnCompletions.set(turnId, { item });
  }

  const message = describeAppServerNotification(item);
  if (item.method.endsWith('/outputDelta')
      || item.method.endsWith('/textDelta')
      || item.method.endsWith('/summaryTextDelta')) return;
  if (!message) return;
  emit({
    type: item.method === 'account/rateLimits/updated' ? 'rate-limits' : 'progress',
    message,
    threadId,
    turnId,
    event: item,
    rateLimits: item.method === 'account/rateLimits/updated' ? params.rateLimits : undefined
  });
}

function findActiveTurn(turnId) {
  if (!turnId) return undefined;
  return Array.from(activeAppServerTurns.values()).find(active => active.turnId === turnId);
}

function completeAppServerTurn(completion) {
  const item = completion.item;
  const params = item.params ?? {};
  const active = completion.active ?? findActiveTurn(params.turnId ?? params.turn?.id);
  if (!active) return;
  flushAssistantDelta(active);
  activeAppServerTurns.delete(active.threadId);
  const status = params.turn?.status ?? 'completed';
  const error = params.turn?.error;
  if (status === 'failed') {
    active.completion.reject(new Error(error?.message ?? JSON.stringify(error ?? params.turn)));
    return;
  }
  active.completion.resolve({
    threadId: active.threadId,
    turnId: active.turnId,
    operationId: active.operationId,
    finalResponse: active.finalResponse,
    result: { turn: params.turn, items: active.items, finalResponse: active.finalResponse }
  });
}

function queueAssistantDelta(active, delta) {
  if (!delta) return;
  active.pendingDelta += delta;
  if (active.deltaTimer) return;
  active.deltaTimer = setTimeout(() => flushAssistantDelta(active), 75);
}

function flushAssistantDelta(active) {
  if (active.deltaTimer) {
    clearTimeout(active.deltaTimer);
    active.deltaTimer = null;
  }
  if (!active.pendingDelta) return;
  emit({
    type: 'assistant-delta',
    message: active.pendingDelta,
    threadId: active.threadId,
    turnId: active.turnId,
    operationId: active.operationId
  });
  active.pendingDelta = '';
}

function describeServerRequest(item) {
  if (item.method === 'item/commandExecution/requestApproval') return 'Codex is waiting for command approval.';
  if (item.method === 'item/fileChange/requestApproval') return 'Codex is waiting for file-change approval.';
  if (item.method === 'item/permissions/requestApproval') return 'Codex is waiting for permission approval.';
  if (item.method === 'item/tool/requestUserInput') return 'Codex needs input before it can continue.';
  return 'Codex is waiting for a response: ' + item.method;
}

function describeAppServerNotification(item) {
  const params = item.params ?? {};
  if (item.method === 'thread/started') return 'Started Codex thread';
  if (item.method === 'turn/started') return 'Codex is working...';
  if (item.method === 'turn/completed') return params.turn?.status === 'interrupted' ? 'Codex turn stopped' : 'Codex turn completed';
  if (item.method === 'item/started') return 'Codex started ' + (params.item?.type ?? 'an item');
  if (item.method === 'item/completed') return 'Codex completed ' + (params.item?.type ?? 'an item');
  if (item.method === 'account/rateLimits/updated') return 'Codex rate limits updated';
  if (item.method === 'error') return params.error?.message ?? params.message ?? 'Codex app-server error';
  return '';
}

function throwOnRpcError(response, action) {
  if (response?.error) throw new Error('Codex app-server could not ' + action + ': ' + JSON.stringify(response.error));
}

function createDeferred() {
  let resolve;
  let reject;
  const promise = new Promise((resolveValue, rejectValue) => {
    resolve = resolveValue;
    reject = rejectValue;
  });
  return { promise, resolve, reject };
}

async function runSdkThread(thread, request) {
  if (typeof thread.runStreamed === 'function') {
    const state = createCodexEventState(request.threadId);
    const streamed = await thread.runStreamed(buildInput(request), buildRunOptions(request));
    for await (const event of streamed.events) {
      processCodexEventObject(event, state);
      emitCodexProgress(event);
    }

    state.threadId = state.threadId ?? thread.id ?? request.threadId;
    return finalizeCodexEventState(state);
  }

  const result = await thread.run(buildInput(request), buildRunOptions(request));
  const threadId = result?.threadId ?? result?.thread_id ?? thread.id ?? request.threadId;
  return { threadId, finalResponse: result?.final_response ?? result?.finalResponse ?? String(result ?? ''), result };
}

async function getRateLimits() {
  const child = spawn(resolveCodexExecutable(), ['app-server', '--listen', 'stdio://'], {
    env: process.env,
    stdio: ['pipe', 'pipe', 'pipe']
  });

  let stderr = '';
  child.stderr.on('data', data => { stderr += data.toString(); });
  const rpc = createJsonRpcClient(child, 15000);
  try {
    await rpc.send({ jsonrpc: '2.0', id: 1, method: 'initialize', params: { clientInfo: { name: 'VSCodex', version: '0.4.3' }, capabilities: { experimentalApi: true } } });
    const response = await rpc.send({ jsonrpc: '2.0', id: 2, method: 'account/rateLimits/read' });
    if (response.error) throw new Error(JSON.stringify(response.error));
    if (!response.result && stderr) throw new Error(stderr.trim());
    return response.result ?? {};
  } finally {
    rpc.close();
    try { child.kill(); } catch {}
  }
}

function createJsonRpcClient(child, timeoutMs, onMessage) {
  const stdout = readline.createInterface({ input: child.stdout, crlfDelay: Infinity });
  const pending = new Map();
  let closed = false;

  const rejectAll = error => {
    for (const entry of pending.values()) {
      clearTimeout(entry.timer);
      entry.reject(error);
    }
    pending.clear();
  };

  child.once('error', rejectAll);
  child.once('close', code => {
    if (code !== 0) rejectAll(new Error('Codex app-server exited before returning rate limits. Exit code: ' + code));
  });

  stdout.on('line', line => {
    if (!line.trim().startsWith('{')) return;
    let item;
    try { item = JSON.parse(line); } catch { return; }
    if (item.method) onMessage?.(item);
    const entry = pending.get(item.id);
    if (!entry) return;
    pending.delete(item.id);
    clearTimeout(entry.timer);
    entry.resolve(item);
  });

  return {
    send(request) {
      if (closed) return Promise.reject(new Error('Codex app-server JSON-RPC client is closed.'));
      return new Promise((resolve, reject) => {
        const timer = setTimeout(() => {
          pending.delete(request.id);
          reject(new Error('Timed out reading Codex account rate limits.'));
        }, timeoutMs);
        pending.set(request.id, { resolve, reject, timer });
        child.stdin.write(JSON.stringify(request) + '\n');
      });
    },
    respond(id, result) {
      if (closed) return Promise.reject(new Error('Codex app-server JSON-RPC client is closed.'));
      child.stdin.write(JSON.stringify({ jsonrpc: '2.0', id, result }) + '\n');
      return Promise.resolve({ responded: true });
    },
    close() {
      closed = true;
      stdout.close();
      try { child.stdin.end(); } catch {}
      rejectAll(new Error('Codex app-server JSON-RPC client closed.'));
    }
  };
}

function emitCodexProgress(event) {
  if (!event || typeof event !== 'object') return;
  if (event.type === 'codex.rate_limits' || event.rate_limits || event.rateLimits) {
    emit({ type: 'rate-limits', message: 'Codex rate limits updated', rateLimits: event.rate_limits ?? event.rateLimits ?? event });
    return;
  }

  const message = describeCodexEvent(event);
  if (message) emit({ type: 'progress', message, event });
}

function describeCodexEvent(event) {
  if (event.type === 'thread.started') return 'Started Codex thread';
  if (event.type === 'turn.started') return 'Codex is working...';
  if (event.type === 'item.started') return 'Codex started ' + (event.item?.type ?? 'an item');
  if (event.type === 'item.completed' && event.item?.type !== 'agent_message') return 'Codex completed ' + (event.item?.type ?? 'an item') + '; you can queue the next prompt';
  if (event.type === 'turn.completed') return 'VSCodex is finalizing the response; you can queue the next prompt';
  return '';
}

function isSdkJsonNoiseError(error) {
  const message = error?.stack ?? error?.message ?? String(error ?? '');
  return message.includes('Failed to parse item: SUCCESS: The process with PID');
}

function resolveCodexExecutable() {
  if (process.env.CODEX_CLI_PATH && existsSync(process.env.CODEX_CLI_PATH)) return process.env.CODEX_CLI_PATH;
  const npmRoot = getGlobalNpmRoot();
  const winNativeCandidates = [
    path.join(npmRoot, '@openai', 'codex', 'node_modules', '@openai', 'codex-win32-x64', 'vendor', 'x86_64-pc-windows-msvc', 'bin', 'codex.exe'),
    path.join(npmRoot, '@openai', 'codex-sdk', 'node_modules', '@openai', 'codex-win32-x64', 'vendor', 'x86_64-pc-windows-msvc', 'bin', 'codex.exe'),
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
  const { model, effort } = normalizeModelAndReasoningEffort(request);
  const args = ['exec', '--experimental-json'];
  if (model) args.push('--model', model);
  if (request.approvalPolicy) args.push('--config', 'approval_policy=' + JSON.stringify(normalizeApprovalPolicy(request.approvalPolicy)));
  args.push('--config', 'model_reasoning_effort=' + JSON.stringify(effort));
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
    cwd: request.workspaceRoot,
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
function closeAppServer() {
  try { appServer?.rpc?.close(); } catch {}
  try { appServer?.child?.kill(); } catch {}
}
process.once('SIGINT', () => { closeAppServer(); process.exit(130); });
process.once('SIGTERM', () => { closeAppServer(); process.exit(143); });
process.once('exit', closeAppServer);
emit({ type: 'ready', message: 'Codex SDK bridge ready' });
