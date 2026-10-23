// Executes the packaged public thread-management adapter against deterministic RPC responses.
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
const source = readFileSync(process.argv[2], 'utf8');
const start = source.indexOf('async function manageThread(request)');
if (start < 0) throw new Error('Thread management adapter is missing.');
const calls = [];
let mode = 'normal';
let id = 0;
const context = vm.createContext({
  ensureAppServer: async () => ({ rpc: { send: async request => {
    calls.push(request);
    if (mode === 'failure') return { error: { message: 'archive denied' } };
    if (request.method === 'thread/list') {
      if (mode === 'repeat') return { result: { data: [], nextCursor: 'loop' } };
      return { result: request.params.cursor === null
        ? { data: [{ id: 'a', preview: 'old' }], nextCursor: 'page2' }
        : { data: [{ id: 'a', preview: 'new' }, { id: 'b' }], nextCursor: null } };
    }
    return { result: { thread: { id: request.method === 'thread/fork' ? 'forked' : request.params.threadId } } };
  } } }),
  nextAppServerId: () => ++id,
  throwOnRpcError: response => { if (response.error) throw new Error(response.error.message); }
});
vm.runInContext(source.slice(start), context);
const catalog = await context.manageThread({ command: 'listThreads', archived: true });
const catalogCalls = calls.splice(0);
await context.manageThread({ command: 'readThread', threadId: 'a' });
const fork = await context.manageThread({ command: 'forkThread', threadId: 'a' });
await context.manageThread({ command: 'archiveThread', threadId: 'a', archived: true });
await context.manageThread({ command: 'archiveThread', threadId: 'a', archived: false });
const operations = calls.splice(0);
mode = 'failure';
let failure = '';
try { await context.manageThread({ command: 'archiveThread', threadId: 'a', archived: true }); }
catch (error) { failure = error.message; }
const mutationAttempts = calls.splice(0).length;
mode = 'repeat';
let paginationFailure = '';
try { await context.manageThread({ command: 'listThreads' }); }
catch (error) { paginationFailure = error.message; }
let missingIdFailure = '';
try { await context.manageThread({ command: 'forkThread', threadId: '  ' }); }
catch (error) { missingIdFailure = error.message; }
process.stdout.write(JSON.stringify({ catalog, catalogCalls, operations, fork, failure, mutationAttempts, paginationFailure, missingIdFailure }));
