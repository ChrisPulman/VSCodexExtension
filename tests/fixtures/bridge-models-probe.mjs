import { readFileSync } from 'node:fs';
import vm from 'node:vm';

// Run the packaged pagination and normalization functions against an in-memory RPC server.
const source = readFileSync(process.argv[2], 'utf8');
const functions = source.slice(source.indexOf('async function getModels()'), source.indexOf('function createJsonRpcClient('));
const normalization = source.slice(source.indexOf('const reasoningEffortOrder ='), source.indexOf('function normalizeServiceTier('));
const requests = [];
let mode = 'pages';
const pages = [
  { data: [{ model: 'future-model', isDefault: true, defaultReasoningEffort: 'high', supportedReasoningEfforts: [{ reasoningEffort: 'low' }, { reasoningEffort: 'high' }, { reasoningEffort: 'ultra' }] }], nextCursor: 'page-2' },
  { data: [{ model: 'second-model', defaultReasoningEffort: 'none', supportedReasoningEfforts: [{ reasoningEffort: 'none' }] }, { model: 'hidden-model', hidden: true }], nextCursor: null }
];
const context = vm.createContext({
  normalizeLower: value => String(value ?? '').toLowerCase(),
  nextAppServerId: () => requests.length + 1,
  throwOnRpcError(response) { if (response.error) throw new Error(response.error.message); },
  ensureAppServer: async () => ({ rpc: { send: async request => {
    requests.push(request);
    if (mode === 'repeat') return { result: { data: [], nextCursor: 'same' } };
    if (mode === 'error') return { error: { message: 'model access denied' } };
    return { result: pages[request.params.cursor === 'page-2' ? 1 : 0] };
  } } })
});
vm.runInContext(normalization + '\n' + functions, context);
const result = await context.getModels();
const efforts = [context.normalizeReasoningEffort('future-model', 'ultra'), context.normalizeReasoningEffort('future-model', 'medium'), context.normalizeReasoningEffort('second-model', 'high')];
mode = 'repeat';
const repeatedCursor = await context.getModels().then(() => '', error => error.message);
mode = 'error';
const rpcError = await context.getModels().then(() => '', error => error.message);
process.stdout.write(JSON.stringify({ result, efforts, requests: requests.slice(0, 2), repeatedCursor, rpcError }));
