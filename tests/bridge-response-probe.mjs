// Runs the actual packaged response function with an in-memory app-server RPC endpoint.
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
const source = readFileSync(process.argv[2], 'utf8');
const start = source.indexOf('function respondToAppServerRequest(request)');
const end = source.indexOf('function processAppServerMessage(item)', start);
if (start < 0 || end < 0) throw new Error('Cannot locate packaged response function.');
const responses = [];
const context = vm.createContext({ appServer: { rpc: { respond(id, result) { responses.push({ id, result }); return {}; } } } });
vm.runInContext(source.slice(start, end), context);
context.respondToAppServerRequest({ requestId: 0, result: { action: 'accept', content: {} } });
context.respondToAppServerRequest({ requestId: '0', result: { action: 'decline', content: null } });
let rejectedMissing = 0;
for (const requestId of [undefined, null]) {
  try { context.respondToAppServerRequest({ requestId }); }
  catch (error) { if (error.message.includes('identifier is required')) rejectedMissing++; else throw error; }
}
// Exercise the real dispatch/fallback boundary with controlled RPC outcomes.
const handleStart = source.indexOf('async function handle(request)');
const handleEnd = source.indexOf('async function ensureAppServer()', handleStart);
const turnStart = source.indexOf('async function runAppServerTurn(request)');
const turnEnd = source.indexOf('function buildThreadStartParams(request)', turnStart);
let sdkRuns = 0;
let rpcCalls = 0;
let startupFails = false;
let resumeFails = false;
const activeTurns = new Map();
const boundary = vm.createContext({
  activeAppServerTurns: activeTurns,
  ensureAppServer: async () => {
    if (startupFails) throw new Error('startup unavailable');
    return { rpc: { send: async () => {
      rpcCalls++;
      if (resumeFails) return { error: { message: 'thread already has an active writer' } };
      return { result: { thread: { id: 'busy' } } };
    } } };
  },
  buildThreadResumeParams: () => ({}), buildThreadStartParams: () => ({}),
  nextAppServerId: () => 'test',
  throwOnRpcError: response => { if (response.error) throw new Error(response.error.message); },
  emit: () => {}, getThread: async () => ({}), AbortController,
  runSdkThread: async () => { sdkRuns++; return { finalResponse: 'startup fallback' }; },
  threads: new Map()
});
vm.runInContext(source.slice(handleStart, handleEnd) + source.slice(turnStart, turnEnd), boundary);
async function capture(request) {
  try { await boundary.handle(request); return ''; }
  catch (error) { return error.message; }
}
activeTurns.set('busy', {});
const activeConflict = await capture({ threadId: 'busy', workspaceRoot: 'workspace' });
const activeConflictRpcCalls = rpcCalls;
activeTurns.clear();
resumeFails = true;
const resumeConflict = await capture({ threadId: 'busy', workspaceRoot: 'workspace' });
resumeFails = false;
// The server can return an existing thread ID even without a requested ID.
activeTurns.set('busy', {});
const serverThreadConflict = await capture({ workspaceRoot: 'workspace' });
const conflictSdkRuns = sdkRuns;
activeTurns.clear();
startupFails = true;
const startupFallback = await boundary.handle({ workspaceRoot: 'workspace' });
process.stdout.write(JSON.stringify({ responses, rejectedMissing, activeConflict, activeConflictRpcCalls,
  resumeConflict, serverThreadConflict, conflictSdkRuns, startupFallback: startupFallback.finalResponse, sdkRuns }));
