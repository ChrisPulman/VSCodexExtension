import fs from 'node:fs';
import vm from 'node:vm';
import readline from 'node:readline';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { EventEmitter } from 'node:events';
import { PassThrough, Writable } from 'node:stream';

const source = fs.readFileSync(process.argv[2], 'utf8').replace(/^\uFEFF?#![^\n]*\n/, '').replace(/^import .*;\r?\n/gm, '');
const sent = [];
const emitted = [];
let spawned = 0;
let api;
const fakeSpawn = () => {
  spawned++;
  const child = new EventEmitter();
  child.stdout = new PassThrough();
  child.stderr = new PassThrough();
  child.exitCode = null;
  child.killed = false;
  child.kill = () => { child.killed = true; child.emit('close', 0); };
  const output = messages => child.stdout.write(messages.map(message => JSON.stringify(message)).join('\n') + '\n');
  child.stdin = new Writable({
    write(chunk, encoding, callback) {
      const request = JSON.parse(chunk.toString('utf8'));
      sent.push(request);
      if (request.method === 'initialize') output([{ id: request.id, result: {} }]);
      else if (request.method === 'thread/start') output([{ id: request.id, result: { thread: { id: 'thread-test' } } }]);
      else if (request.method === 'turn/start') {
        const response = { id: request.id, result: { turn: { id: 'turn-test' } } };
        if (request.params.input[0].text === 'elicitation') {
          output([response, { id: 0, method: 'mcpServer/elicitation/request', params: { threadId: 'thread-test', mode: 'form', requestedSchema: { type: 'object', properties: {} } } }]);
        } else if (request.params.input[0].text === 'failure') {
          output([response, { method: 'turn/completed', params: { threadId: 'thread-test', turn: { id: 'turn-test', status: 'failed', error: { message: 'simulated turn failure' } } } }]);
        } else {
          output([response, { method: 'item/agentMessage/delta', params: { threadId: 'thread-test', turnId: 'turn-test', delta: 'Überprüfung 日本語 😀' } }, { method: 'turn/completed', params: { threadId: 'thread-test', turn: { id: 'turn-test', status: 'completed' } } }]);
        }
      } else if (request.id === 0 && request.result) {
        output([{ method: 'item/agentMessage/delta', params: { threadId: 'thread-test', turnId: 'turn-test', delta: 'Approved ✓' } }, { method: 'turn/completed', params: { threadId: 'thread-test', turn: { id: 'turn-test', status: 'completed' } } }]);
      }
      callback();
    }
  });
  return child;
};
const fakeProcess = {
  argv: [], env: { CODEX_CLI_PATH: 'mock-codex' }, platform: 'win32',
  stdin: new PassThrough(), once() {}, exit() {},
  stdout: { write(line) {
    const event = JSON.parse(line);
    emitted.push(event);
    if (event.type === 'approval-request') queueMicrotask(() => api.handle({ command: 'respondServerRequest', requestId: event.requestId, result: { action: 'accept', content: {} } }));
  } }
};
const context = vm.createContext({
  readline, spawn: fakeSpawn, execFileSync() { throw new Error('Unexpected external execution'); },
  existsSync: value => value === 'mock-codex', readFileSync: fs.readFileSync, pathToFileURL, path,
  process: fakeProcess, console, setTimeout, clearTimeout, AbortController, queueMicrotask
});
api = await vm.runInContext(`(async () => { ${source}\nreturn { handle, closeAppServer }; })()`, context);
const deadline = setTimeout(() => { console.error('Bridge turn probe timed out'); process.exit(1); }, 5000);
try {
  const normal = await api.handle({ workspaceRoot: 'C:/workspace', prompt: 'normal' });
  const elicitation = await api.handle({ workspaceRoot: 'C:/workspace', prompt: 'elicitation' });
  const failure = await api.handle({ workspaceRoot: 'C:/workspace', prompt: 'failure' }).then(() => 'resolved', error => error.message);
  console.log(JSON.stringify({ normal: normal.finalResponse, elicitation: elicitation.finalResponse, failure, methods: sent.filter(item => item.method).map(item => item.method), responseId: sent.find(item => item.id === 0)?.id, spawned, fallbackCount: emitted.filter(item => item.type === 'transport-fallback').length }));
} finally {
  clearTimeout(deadline);
  api.closeAppServer();
}
